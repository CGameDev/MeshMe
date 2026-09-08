// SPDX-License-Identifier: AGPL-3.0-only
using System.Security.Cryptography;
using System.Text.Json;

namespace MeshMe360.Core.Licensing;

public sealed record SignedEntitlement(string Payload, string Signature, string KeyId);
public sealed record Entitlement(int Version, string Product, string Type, string Id, HardwareFingerprint Hardware,
    long IssuedAt, long ExpiresAt, long NextValidation, string KeyId, string Nonce);
public interface IEntitlementTokenVerifier
{
    Entitlement Verify(SignedEntitlement token, HardwareFingerprint hardware, DateTimeOffset now);
}
public sealed class EntitlementTokenVerifier(IReadOnlyDictionary<string, string> publicKeys) : IEntitlementTokenVerifier
{
    public Entitlement Verify(SignedEntitlement token, HardwareFingerprint hardware, DateTimeOffset now)
    {
        try
        {
            if (token.Payload.Length > 16384 || token.Signature.Length > 1024 || !publicKeys.TryGetValue(token.KeyId, out var pem)) throw new InvalidDataException("Unknown signing key.");
            using var rsa = RSA.Create(); rsa.ImportFromPem(pem);
            var bytes = Convert.FromBase64String(token.Payload);
            if (rsa.KeySize < 3072 || !rsa.VerifyData(bytes, Convert.FromBase64String(token.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new InvalidDataException("Invalid entitlement signature.");
            var value = JsonSerializer.Deserialize<Entitlement>(bytes, BridgeMessage.Json) ?? throw new InvalidDataException("Missing entitlement.");
            var utc = now.ToUnixTimeSeconds();
            if (value.Version != 1 || value.Product != "meshme360" || value.Type is not ("trial" or "license") || value.KeyId != token.KeyId ||
                string.IsNullOrWhiteSpace(value.Id) || string.IsNullOrWhiteSpace(value.Nonce) || value.IssuedAt > utc + 300 || value.ExpiresAt <= utc ||
                value.ExpiresAt <= value.IssuedAt || value.NextValidation < value.IssuedAt || value.NextValidation > value.ExpiresAt ||
                value.ExpiresAt - value.IssuedAt > (value.Type == "trial" ? 7 : 30) * 86400L || !value.Hardware.Matches(hardware))
                throw new InvalidDataException("Entitlement expired, belongs to another PC, or has invalid bounds. Connect to validate.");
            return value;
        }
        catch (Exception error) when (error is FormatException or CryptographicException or JsonException or ArgumentException or NullReferenceException)
        { throw new InvalidDataException("Invalid entitlement.", error); }
    }
}

public sealed record LicenseCache(SignedEntitlement? Token, long TrustedUtc, long LastAttemptUtc, bool Denied);
public sealed record LicenseState(bool CanEdit, string Message, Entitlement? Entitlement = null);
public interface ILicenseCache { Task<LicenseCache?> ReadAsync(); Task WriteAsync(LicenseCache value); }
public interface IActivationApiClient
{
    Task<SignedEntitlement?> SendAsync(string action, HardwareFingerprint hardware, string? key, SignedEntitlement? token);
}
public sealed class ActivationException(string code, string message) : Exception(message) { public string Code { get; } = code; }
public interface ILicenseService
{
    Task<LicenseState> RefreshAsync(bool force = false);
    Task<LicenseState> StartTrialAsync();
    Task<LicenseState> ActivateAsync(string key);
    Task DeactivateAsync();
}

public sealed class LicenseService(IHardwareFingerprintService hardware, IActivationApiClient api, IEntitlementTokenVerifier verifier,
    ILicenseCache cache, Func<DateTimeOffset>? clock = null) : ILicenseService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();
    public Task<LicenseState> StartTrialAsync() => RequestAsync("trial/start", null);
    public Task<LicenseState> ActivateAsync(string key) => ActivationKey.IsValid(key) ? RequestAsync("activate", ActivationKey.Normalize(key)) :
        Task.FromResult(new LicenseState(false, "Enter a valid 25-symbol activation key; check for a typing error."));
    private async Task<LicenseState> RequestAsync(string action, string? key)
    {
        await gate.WaitAsync();
        try
        {
            var pc = await hardware.GetAsync(); pc.Validate();
            var token = await api.SendAsync(action, pc, key, null) ?? throw new InvalidDataException("Missing signed entitlement.");
            var value = verifier.Verify(token, pc, Now);
            await cache.WriteAsync(new(token, Math.Max(value.IssuedAt, Now.ToUnixTimeSeconds()), Now.ToUnixTimeSeconds(), false));
            return new(true, value.Type == "trial" ? "7-day trial active." : "License active.", value);
        }
        catch (Exception error) when (error is ActivationException or InvalidDataException or HttpRequestException or TaskCanceledException)
        { return new(false, error is ActivationException ? error.Message : "Activation could not be completed. Check your connection and computer clock, then retry."); }
        finally { gate.Release(); }
    }
    public async Task<LicenseState> RefreshAsync(bool force = false)
    {
        await gate.WaitAsync();
        try
        {
            LicenseCache? saved;
            try { saved = await cache.ReadAsync(); } catch (Exception e) when (e is IOException or CryptographicException or JsonException) { saved = null; }
            if (saved?.Token is null) return new(false, "Start a 7-day trial or activate your license.");
            var pc = await hardware.GetAsync(); var now = Now; Entitlement? value = null;
            var rollback = now.ToUnixTimeSeconds() + 300 < saved.TrustedUtc;
            try { value = verifier.Verify(saved.Token, pc, now); } catch (InvalidDataException) { }
            var due = force || rollback || saved.Denied || value is null || now.ToUnixTimeSeconds() >= value.NextValidation;
            if (due && (force || now.ToUnixTimeSeconds() >= saved.LastAttemptUtc + 86400 || rollback))
            {
                saved = saved with { LastAttemptUtc = now.ToUnixTimeSeconds() };
                await cache.WriteAsync(saved);
                try
                {
                    var token = await api.SendAsync("validate", pc, null, saved.Token) ?? throw new InvalidDataException("Missing entitlement.");
                    value = verifier.Verify(token, pc, now);
                    saved = new(token, Math.Max(value.IssuedAt, now.ToUnixTimeSeconds()), now.ToUnixTimeSeconds(), false);
                    rollback = false;
                }
                catch (ActivationException error) when (error.Code is "KEY_REVOKED" or "KEY_SUSPENDED" or "DEVICE_CHANGED" or "TRIAL_EXPIRED" or "INVALID_KEY")
                { await cache.WriteAsync(saved with { Denied = true }); return new(false, error.Message); }
                catch (Exception error) when (error is ActivationException or HttpRequestException or TaskCanceledException or InvalidDataException) { /* An outage never shortens a verified lease. */ }
            }
            if (rollback || saved.Denied || value is null) return new(false, "Online validation is required. Your projects and recovery files are preserved.");
            await cache.WriteAsync(saved with { TrustedUtc = Math.Max(saved.TrustedUtc, now.ToUnixTimeSeconds()) });
            return new(true, value.Type == "trial" ? $"Trial: {Math.Max(0, Math.Ceiling((value.ExpiresAt - now.ToUnixTimeSeconds()) / 86400.0))} day(s) remaining; expires {DateTimeOffset.FromUnixTimeSeconds(value.ExpiresAt):u}." : $"License valid through {DateTimeOffset.FromUnixTimeSeconds(value.ExpiresAt):u}.", value);
        }
        finally { gate.Release(); }
    }
    public async Task DeactivateAsync()
    {
        await gate.WaitAsync();
        try
        {
            var saved = await cache.ReadAsync();
            if (saved?.Token is null) return;
            await api.SendAsync("deactivate", await hardware.GetAsync(), null, saved.Token);
            await cache.WriteAsync(saved with { Token = null, Denied = true });
        }
        finally { gate.Release(); }
    }
}
