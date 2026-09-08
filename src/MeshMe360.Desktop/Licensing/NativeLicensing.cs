// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using MeshMe360.Core;
using MeshMe360.Core.Licensing;

namespace MeshMe360.Desktop.Licensing;

internal sealed class WindowsHardwareFingerprint : IHardwareFingerprintService
{
    public Task<HardwareFingerprint> GetAsync() => Task.Run(() =>
    {
        var raw = new Dictionary<string, string?>();
        foreach (var (name, table, property) in new[] { ("systemUuid", "Win32_ComputerSystemProduct", "UUID"), ("baseboard", "Win32_BaseBoard", "SerialNumber"), ("bios", "Win32_BIOS", "SerialNumber") })
        {
            object? locator = null, service = null, rows = null;
            try
            {
                locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator")!);
                service = ((dynamic)locator!).ConnectServer(".", "root\\cimv2");
                rows = ((dynamic)service).ExecQuery($"SELECT {property} FROM {table}");
                foreach (dynamic row in (System.Collections.IEnumerable)rows)
                {
                    try { raw[name] = Convert.ToString(row.Properties_.Item(property).Value); } finally { Marshal.FinalReleaseComObject(row); }
                    break;
                }
            }
            catch (Exception e) when (e is COMException or UnauthorizedAccessException) { /* Missing components stay missing. */ }
            finally { foreach (var item in new[] { rows, service, locator }) if (item is not null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }
        }
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        raw["machineGuid"] = key?.GetValue("MachineGuid") as string;
        return HardwareFingerprint.Create(raw);
    });
}

internal sealed class DpapiLicenseCache(string directory) : ILicenseCache
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Protect(byte[] bytes, bool encrypt)
    {
        var input = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }; Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var ok = encrypt ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException("Entitlement cache cannot be opened for this Windows user.");
            var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
    public async Task<LicenseCache?> ReadAsync()
    {
        var path = Path.Combine(directory, "entitlement.dat");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Invalid cache size.");
        return JsonSerializer.Deserialize<LicenseCache>(Protect(await File.ReadAllBytesAsync(path), false), BridgeMessage.Json);
    }
    public async Task WriteAsync(LicenseCache value)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "entitlement.dat"); var staging = path + "." + Guid.NewGuid().ToString("N");
        try { await File.WriteAllBytesAsync(staging, Protect(JsonSerializer.SerializeToUtf8Bytes(value, BridgeMessage.Json), true)); File.Move(staging, path, true); }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}

internal sealed record ActivationConfiguration(string Endpoint, Dictionary<string, string> PublicKeys);
internal sealed class ActivationApiClient(Uri endpoint) : IActivationApiClient
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 32768 };
    public async Task<SignedEntitlement?> SendAsync(string action, HardwareFingerprint hardware, string? key, SignedEntitlement? token)
    {
        if (endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo) || action is not ("trial/start" or "activate" or "validate" or "deactivate")) throw new InvalidDataException("HTTPS activation is required.");
        using var response = await http.PostAsJsonAsync(new Uri(endpoint, "api/v1/" + action), new
            { version = 1, product = "meshme360", appVersion = typeof(ActivationApiClient).Assembly.GetName().Version?.ToString(), hardware, key, token }, BridgeMessage.Json);
        JsonElement body;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType != "application/json") throw new JsonException();
            body = await response.Content.ReadFromJsonAsync<JsonElement>(BridgeMessage.Json);
            if (body.ValueKind != JsonValueKind.Object) throw new JsonException();
        }
        catch (JsonException) { throw new ActivationException("SERVICE_UNAVAILABLE", "The activation service returned an invalid response. Retry later."); }
        if (!response.IsSuccessStatusCode)
        {
            var code = body.TryGetProperty("code", out var c) ? c.GetString() ?? "SERVICE_UNAVAILABLE" : "SERVICE_UNAVAILABLE";
            // Do not display or log arbitrary response bodies, keys or server exceptions.
            var message = code switch
            {
                "DEVICE_LIMIT_REACHED" => "This key is active on another PC. Deactivate that PC or request an administrator reset.",
                "KEY_REVOKED" or "KEY_SUSPENDED" => "This license was revoked or suspended. Contact support.",
                "TRIAL_EXPIRED" or "TRIAL_ALREADY_USED" => "This PC has already used its 7-day trial. Activate a license to continue editing.",
                "DEVICE_CHANGED" => "The hardware identity changed or is insufficient. Contact support for a device reset.",
                "INVALID_KEY" => "The activation key or entitlement is invalid.",
                "RATE_LIMITED" => "Too many attempts. Wait before trying again.",
                _ => "The activation service is unavailable. Check your connection and retry."
            };
            throw new ActivationException(code, message);
        }
        if (action == "deactivate")
        {
            if (!body.TryGetProperty("deactivated", out var deactivated) || deactivated.ValueKind != JsonValueKind.True) throw new ActivationException("SERVICE_UNAVAILABLE", "Deactivation was not confirmed. Retry later.");
            return null;
        }
        try { return body.GetProperty("token").Deserialize<SignedEntitlement>(BridgeMessage.Json); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new ActivationException("SERVICE_UNAVAILABLE", "The activation service returned an invalid response. Retry later."); }
    }
}

internal sealed class UnconfiguredActivation : IActivationApiClient
{
    public Task<SignedEntitlement?> SendAsync(string action, HardwareFingerprint hardware, string? key, SignedEntitlement? token) =>
        throw new ActivationException("SERVICE_UNAVAILABLE", "This build has no activation endpoint/public key configured. Contact the publisher for a configured official build.");
}
