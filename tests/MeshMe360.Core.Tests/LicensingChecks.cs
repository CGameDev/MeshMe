// SPDX-License-Identifier: AGPL-3.0-only
using MeshMe360.Core;
using MeshMe360.Core.Licensing;
using System.Security.Cryptography;
using System.Text.Json;

internal static class LicensingChecks
{
    internal static async Task Run()
    {
        var count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        var raw = new Dictionary<string, string?> { ["systemUuid"] = "a0232543-3888-4947-9000-f0fc0c215303", ["baseboard"] = "BOARD-ALPHA", ["bios"] = "BIOS-ALPHA", ["machineGuid"] = "aafc2543-1888-9947-9000-f0fc0c215303" };
        var pc = HardwareFingerprint.Create(raw);
        Check(pc.Components.Count == 4 && !JsonSerializer.Serialize(pc).Contains("BOARD-ALPHA"), "Hardware components are hashed locally");
        Check(pc.Matches(HardwareFingerprint.Create(raw)), "Same PC recognized");
        var reinstall = new Dictionary<string, string?>(raw) { ["machineGuid"] = Guid.NewGuid().ToString() };
        Check(pc.Matches(HardwareFingerprint.Create(reinstall)), "Windows reinstall tolerates changed MachineGuid");
        var replacement = new Dictionary<string, string?>(raw) { ["systemUuid"] = Guid.NewGuid().ToString(), ["baseboard"] = "BOARD-BETA" };
        var other = HardwareFingerprint.Create(replacement);
        Check(!pc.Matches(other), "Motherboard replacement rejected");
        Check(!pc.Matches(HardwareFingerprint.Create(new Dictionary<string,string?>(raw) { ["baseboard"] = "BOARD-BETA" })), "Strong conflict cannot be overridden by copied weak IDs");
        var missing = HardwareFingerprint.Create(new Dictionary<string, string?>(raw) { ["baseboard"] = "To be filled by O.E.M." });
        Check(missing.Components.Count == 3 && pc.Matches(missing), "OEM placeholder omitted; sufficient remaining identity matches");
        Check(HardwareFingerprint.Create(new Dictionary<string,string?> { ["systemUuid"] = Guid.Empty.ToString(), ["baseboard"] = "00000000", ["bios"] = "Default string" }).Components.Count == 0, "Invalid hardware placeholders omitted");
        Check(ActivationKey.IsValid("00000-00000-00000-00000-00000"), "25-symbol checksum accepted");
        Check(!ActivationKey.IsValid("00000-00000-00000-00000-00001") && !ActivationKey.IsValid("IIIII-00000-00000-00000-00000"), "Checksum and ambiguous alphabet rejected");
        Check(ActivationKey.Normalize(" abcd-e fgh ") == "ABCDEFGH", "Case, space and hyphen normalization");
        using var rsa = RSA.Create(3072);
        var now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
        SignedEntitlement Sign(Entitlement value)
        {
            var bytes=JsonSerializer.SerializeToUtf8Bytes(value,BridgeMessage.Json);
            return new(Convert.ToBase64String(bytes),Convert.ToBase64String(rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)),"test");
        }
        var verifier = new EntitlementTokenVerifier(new Dictionary<string,string> { ["test"] = rsa.ExportSubjectPublicKeyInfoPem() });
        Entitlement Value(string type="license") => new(1,"meshme360",type,"test-license",pc,now.ToUnixTimeSeconds(),now.AddDays(type=="trial"?7:30).ToUnixTimeSeconds(),now.AddDays(7).ToUnixTimeSeconds(),"test",Guid.NewGuid().ToString());
        var paid = Sign(Value());
        Check(verifier.Verify(paid,pc,now).Type=="license", "RSA-3072 signed entitlement verified");
        void Reject(SignedEntitlement token, HardwareFingerprint current, DateTimeOffset time, string name)
        { try { verifier.Verify(token,current,time); } catch (InvalidDataException) { Check(true,name); return; } throw new Exception("FAIL: "+name); }
        Reject(paid with { Payload=Convert.ToBase64String("tampered"u8.ToArray()) },pc,now,"Tampered payload rejected");
        Reject(paid with { Signature=Convert.ToBase64String(new byte[384]) },pc,now,"Tampered signature rejected");
        Reject(paid,other,now,"Copied token fails on another PC");
        Reject(paid,pc,now.AddDays(30),"Lease expires at exact bound");
        Reject(Sign(Value() with { Product="other" }),pc,now,"Wrong product rejected");
        Reject(Sign(Value() with { ExpiresAt=now.AddDays(31).ToUnixTimeSeconds() }),pc,now,"Lease over 30 days rejected");
        Reject(Sign(Value("trial")),pc,now.AddDays(7),"Trial expires after exactly seven days");
        Check(verifier.Verify(paid,HardwareFingerprint.Create(reinstall),now).Type=="license","Offline binding tolerates Windows reinstall");
        var storage=new MemoryCache(); var api=new FakeApi();var hardware=new FakeHardware(pc);
        var service=new LicenseService(hardware,api,verifier,storage,()=>now);
        Check(!(await service.RefreshAsync()).CanEdit && api.Calls==0,"First launch never starts a trial or sends an activation request");
        api.Result=Sign(Value("trial"));Check((await service.StartTrialAsync()).CanEdit&&api.LastAction=="trial/start","Trial starts only through explicit request");
        var calls=api.Calls;now=now.AddDays(1);Check((await service.RefreshAsync()).CanEdit&&api.Calls==calls,"Trial remains usable offline before expiry");
        now=now.AddDays(-2);api.Error=new HttpRequestException();Check(!(await service.RefreshAsync()).CanEdit,"Clock rollback cannot extend a trial while offline");
        now=DateTimeOffset.FromUnixTimeSeconds(1800000000);api.Error=null;api.Result=Sign(Value());Check((await service.ActivateAsync(new string('0',25))).CanEdit,"Explicit paid activation accepted");
        now=now.AddDays(8);api.Error=new HttpRequestException();Check((await service.RefreshAsync()).CanEdit,"Paid user continues within signed grace during outage");
        api.Error=new ActivationException("KEY_REVOKED","Revoked");Check(!(await service.RefreshAsync(true)).CanEdit,"Explicit revocation fails closed");
        api.Error=new HttpRequestException();Check(!(await service.RefreshAsync(true)).CanEdit,"Revocation persists across later outage");
        api.Error=null;api.Result=Sign(Value());Check((await service.ActivateAsync(new string('0',25))).CanEdit,"Successful activation clears prior denial");
        await service.DeactivateAsync();Check(storage.Value!.Token is null&&!(await service.RefreshAsync()).CanEdit,"Online deactivation clears only entitlement cache");
        Check(GraphicsSettings.BrowserArguments("Automatic")=="" && new AppSettings().RenderingMode=="Automatic","GPU acceleration defaults on");
        Check(GraphicsSettings.BrowserArguments("SoftwareCompatibility")=="--disable-gpu","Compatibility uses documented GPU-disabled path");
        Check(GraphicsSettings.IsGraphicsInitializationFailure("Error creating WebGL context.")&&!GraphicsSettings.IsGraphicsInitializationFailure("Packaged engine files are missing."),"Graphics recovery excludes unrelated startup errors");
        var crossPath=Environment.GetEnvironmentVariable("MESHME_CROSS_TOKEN");
        if(crossPath is not null){var json=JsonDocument.Parse(await File.ReadAllTextAsync(crossPath)).RootElement;var keys=new Dictionary<string,string>{{"cross",json.GetProperty("publicKey").GetString()!}};var token=json.GetProperty("token").Deserialize<SignedEntitlement>(BridgeMessage.Json)!;var crossHardware=json.GetProperty("hardware").Deserialize<HardwareFingerprint>(BridgeMessage.Json)!;Check(new EntitlementTokenVerifier(keys).Verify(token,crossHardware,DateTimeOffset.UtcNow).Product=="meshme360","PHP signed payload verified by .NET RSA verifier");}
        Console.WriteLine($"All {count} licensing/graphics checks passed.");
    }
    private sealed class MemoryCache : ILicenseCache { public LicenseCache? Value;public Task<LicenseCache?> ReadAsync()=>Task.FromResult(Value);public Task WriteAsync(LicenseCache value){Value=value;return Task.CompletedTask;} }
    private sealed class FakeHardware(HardwareFingerprint value) : IHardwareFingerprintService {public Task<HardwareFingerprint> GetAsync()=>Task.FromResult(value);}
    private sealed class FakeApi : IActivationApiClient
    {
        public int Calls;public string? LastAction;public SignedEntitlement? Result;public Exception? Error;
        public Task<SignedEntitlement?> SendAsync(string action,HardwareFingerprint h,string? key,SignedEntitlement? token){Calls++;LastAction=action;if(Error is not null)throw Error;return Task.FromResult(Result);}
    }
}
