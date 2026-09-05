// SPDX-License-Identifier: AGPL-3.0-only
using System.Security.Cryptography;
using System.Text.Json;
using MeshMe360.Core;

var root = Path.Combine(Path.GetTempPath(), "MeshMe360-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var paths = new AppDataPaths(Path.Combine(root, "Roaming"), Path.Combine(root, "Local"));
var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
async Task Reject(Func<Task> operation, string name)
{
    try { await operation(); } catch (Exception error) when (error is InvalidDataException or UnauthorizedAccessException or IOException or JsonException) { Check(true, name); return; }
    throw new Exception("FAIL: expected rejection: " + name);
}
var file = Path.Combine(root, "cübè model.stl");
await File.WriteAllBytesAsync(file, [1, 3, 5, 7]);
var requestId = Guid.NewGuid().ToString();
var message = BridgeMessage.Create("settings.get", requestId, new { });
Check(BridgeMessage.Parse(message.ToJson()).RequestId == requestId, "Envelope preserves request correlation");
await Reject(() => Task.Run(() => BridgeMessage.Parse("{}")), "Malformed envelope rejected");
await Reject(() => Task.Run(() => BridgeMessage.Parse("not JSON")), "Malformed JSON rejected");
Check(HostFiles.ParseArguments([file]).Path == file, "Spaces and Unicode startup path preserved");
Check(HostFiles.ParseArguments(["unused.txt", file]).Warning is not null, "Multiple arguments choose first supported file and warn");
Check(HostFiles.ParseArguments([]).Path is null, "No startup document allowed");
await Reject(() => Task.Run(() => HostFiles.ParseArguments([Path.Combine(root, "missing.obj")])), "Missing startup file rejected");
await Reject(() => Task.Run(() => HostFiles.ParseArguments(["unknown.txt"])), "Unsupported startup file rejected");
var clock = DateTimeOffset.UtcNow;
using (var tokens = new FileTokenStore(() => clock))
{
    var grant = tokens.Issue(file, requestId);
    Check(grant.Token.Length == 64 && !grant.Token.Contains("model"), "Opaque random file grant");
    var (stream, consumed) = tokens.Consume("GET", HostOrigins.Bridge + "/file/" + grant.Token, HostOrigins.App);
    Check(consumed.RequestId == requestId && stream.ReadByte() == 1, "Approved GET yields exact stream and correlation");
    await Reject(() => Task.Run(() => tokens.Consume("GET", HostOrigins.Bridge + "/file/" + grant.Token, HostOrigins.App)), "Consumed token rejected");
    foreach (var route in new[] { "/file/../secret", "/file/%2e%2e/secret", "/file/" + grant.Token + "?path=secret", "/other/" + grant.Token })
        await Reject(() => Task.Run(() => tokens.Consume("GET", HostOrigins.Bridge + route, HostOrigins.App)), "Invalid route rejected: " + route[..Math.Min(route.Length, 22)]);
    grant = tokens.Issue(file, requestId);
    await Reject(() => Task.Run(() => tokens.Consume("POST", HostOrigins.Bridge + "/file/" + grant.Token, HostOrigins.App)), "Wrong HTTP method rejected");
    await Reject(() => Task.Run(() => tokens.Consume("GET", HostOrigins.Bridge + "/file/" + grant.Token, "https://evil.example")), "Wrong origin rejected");
    clock = clock.AddMinutes(6);
    await Reject(() => Task.Run(() => tokens.Consume("GET", HostOrigins.Bridge + "/file/" + grant.Token, HostOrigins.App)), "Expired token rejected");
    grant = tokens.Issue(file, requestId); tokens.RevokeAll();
    Check(!stream.CanRead, "Document invalidation closes live streams");
    await Reject(() => Task.Run(() => tokens.Consume("GET", HostOrigins.Bridge + "/file/" + grant.Token, HostOrigins.App)), "Document invalidation revokes pending tokens");
}
var large = Path.Combine(root, "large.stl");
await using (var output = File.Create(large))
{
    var block = new byte[65536]; RandomNumberGenerator.Fill(block);
    for (var i = 0; i < 2048; i++) await output.WriteAsync(block);
}
using (var tokens = new FileTokenStore())
{
    var grant = tokens.Issue(large, requestId);
    var before = GC.GetAllocatedBytesForCurrentThread();
    var (stream, _) = tokens.Consume("GET", HostOrigins.Bridge + "/file/" + grant.Token, HostOrigins.App);
    Check(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024, "128 MB file opens without file-sized host allocation");
    var actual = await SHA256.HashDataAsync(stream);
    await using var original = File.OpenRead(large);
    Check(actual.SequenceEqual(await SHA256.HashDataAsync(original)), "128 MB transfer is byte-exact");
}
var settings = new SettingsService(paths);
var defaults = await settings.GetAsync();
Check(defaults.Theme == "System" && defaults.Autosave && defaults.AutomaticallyCheckForUpdates && defaults.AutosaveIntervalSeconds == 120, "Exact settings defaults");
await settings.PatchAsync(JsonSerializer.SerializeToElement(new { theme = "Dark", futureSetting = 42 }));
var reloaded = await new SettingsService(paths).GetAsync();
Check(reloaded.Theme == "Dark" && reloaded.Extensions!["futureSetting"].GetInt32() == 42, "Settings restart and unknown-key retention");
await Reject(async () => { await settings.PatchAsync(JsonSerializer.SerializeToElement(new { autosaveIntervalSeconds = 7 })); }, "Invalid settings update rejected atomically");
Check((await settings.GetAsync()).Theme == "Dark" && (await settings.GetAsync()).AutosaveIntervalSeconds == 120, "Rejected update leaves saved state intact");
var recent = new RecentFilesService(paths);
for (var i = 0; i < 22; i++) { var name = Path.Combine(root, $"{i}.stl"); await File.WriteAllTextAsync(name, "fixture"); await recent.AddAsync(name); }
Check((await recent.ListAsync()).Count == 20, "Recent files capped at twenty");
await recent.AddAsync(file); await recent.AddAsync(file.ToUpperInvariant());
Check((await recent.ListAsync()).Count(e => e.Path.Equals(file, StringComparison.OrdinalIgnoreCase)) == 1, "Recent paths deduplicated case-insensitively");
File.Delete(file);
Check((await recent.ListAsync())[0].Missing, "Missing file retained and marked");
await recent.RemoveAsync(file); Check((await recent.ListAsync()).Count == 19, "Missing recent can be removed");
await recent.ClearAsync(); Check((await recent.ListAsync()).Count == 0, "Recent clear persists");
using (var session = new SessionService(paths)) { await session.StartAsync(); } // Simulate unclean exit.
using (var session = new SessionService(paths))
{
    await session.StartAsync(); Check(session.Previous is { Clean: false, Id.Length: > 0 }, "Unclean session detected");
    Check(Directory.GetFiles(Path.Combine(paths.Local, "Session"), "unclean-*.json").Length == 1, "Previous unclean marker preserved");
    await session.CompleteAsync();
}
using (var session = new SessionService(paths)) { await session.StartAsync(); Check(session.Previous!.Clean, "Normal completion marks prior session clean"); await session.CompleteAsync(); }
Check(SaveOperation.FromFilename("test.bumpmesh").Label == "Save Project" && SaveOperation.FromFilename("test.STL").Extension == ".stl", "Native save operation metadata");
await Reject(() => Task.Run(() => SaveOperation.FromFilename("evil.exe")), "Unexpected download extension rejected");
Console.WriteLine($"All {passed} Core checks passed. Test data: {root}");
