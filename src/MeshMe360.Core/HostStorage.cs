// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeshMe360.Core;

public sealed record AppDataPaths(string Roaming, string Local)
{
    public static AppDataPaths ForCurrentUser() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshMe360"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeshMe360"));
}

public sealed class JsonFile<T> where T : new()
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public JsonFile(string path) => _path = path;
    public async Task<T> ReadAsync()
    {
        await _gate.WaitAsync();
        try { return await ReadCoreAsync(); }
        finally { _gate.Release(); }
    }
    private async Task<T> ReadCoreAsync()
    {
        if (!File.Exists(_path)) return new T();
        // Readers keep a stable handle to the old version while an atomic
        // replacement installs the next version, including on Windows.
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync<T>(stream, BridgeMessage.Json) ?? throw new InvalidDataException("Empty settings document.");
    }
    public async Task<T> UpdateAsync(Func<T, T> update)
    {
        await _gate.WaitAsync();
        string? temp = null;
        try
        {
            var value = update(await ReadCoreAsync());
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, value, BridgeMessage.Json);
                await stream.FlushAsync();
                stream.Flush(true);
            }
            File.Move(temp, _path, true);
            return value;
        }
        finally
        {
            if (temp is not null && File.Exists(temp)) File.Delete(temp);
            _gate.Release();
        }
    }
}

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string Theme { get; init; } = "System";
    public string Language { get; init; } = "Auto";
    public bool AutomaticallyCheckForUpdates { get; init; } = true;
    public bool Autosave { get; init; } = true;
    public int AutosaveIntervalSeconds { get; init; } = 120;
    public bool ConfirmTextureDeletion { get; init; } = true;
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
    public void Validate()
    {
        if (SchemaVersion != 1 || !new[] { "System", "Light", "Dark" }.Contains(Theme) || AutosaveIntervalSeconds != 120)
            throw new InvalidDataException("Unsupported settings version, theme or autosave interval.");
        if (!new[] { "Auto", "en", "de", "da", "it", "es", "pt", "fr", "tr", "ja", "ko", "uk", "ru", "zh", "pl" }.Contains(Language))
            throw new InvalidDataException("Unsupported language.");
    }
}

public sealed class SettingsService
{
    private readonly JsonFile<AppSettings> _file;
    public SettingsService(AppDataPaths paths) => _file = new(Path.Combine(paths.Roaming, "settings.json"));
    public async Task<AppSettings> GetAsync() { var settings = await _file.ReadAsync(); settings.Validate(); return settings; }
    public Task<AppSettings> PatchAsync(JsonElement patch) => _file.UpdateAsync(current =>
    {
        if (patch.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Settings patch must be an object.");
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(current, BridgeMessage.Json))!;
        foreach (var item in patch.EnumerateObject()) values[item.Name] = item.Value.Clone();
        var result = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(values), BridgeMessage.Json)!;
        result.Validate();
        return result;
    });
}

public sealed record RecentEntry(string Path, DateTimeOffset OpenedAtUtc, bool Missing = false);
public sealed class RecentFilesService
{
    private readonly JsonFile<List<RecentEntry>> _file;
    public RecentFilesService(AppDataPaths paths) => _file = new(Path.Combine(paths.Roaming, "recent.json"));
    public async Task<List<RecentEntry>> ListAsync() => (await _file.ReadAsync())
        .Select(e => e with { Missing = !File.Exists(e.Path) }).ToList();
    public Task<List<RecentEntry>> AddAsync(string path)
    {
        var full = HostFiles.Validate(path);
        return _file.UpdateAsync(entries => new[] { new RecentEntry(full, DateTimeOffset.UtcNow) }
            .Concat(entries.Where(e => !string.Equals(Path.GetFullPath(e.Path), full, StringComparison.OrdinalIgnoreCase)))
            .Take(20).ToList());
    }
    public Task<List<RecentEntry>> RemoveAsync(string path) => _file.UpdateAsync(entries => entries
        .Where(e => !string.Equals(Path.GetFullPath(e.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)).ToList());
    public Task<List<RecentEntry>> ClearAsync() => _file.UpdateAsync(_ => []);
    public async Task<string> ResolveAsync(string path)
    {
        var entry = (await ListAsync()).FirstOrDefault(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnauthorizedAccessException("File is not in the recent list.");
        return HostFiles.Validate(entry.Path);
    }
}

public sealed record SessionMarker
{
    public string Id { get; init; } = "";
    public DateTimeOffset StartedAtUtc { get; init; }
    public bool Clean { get; init; }
}
public sealed class SessionService : IDisposable
{
    private readonly JsonFile<SessionMarker> _file;
    private readonly string _directory;
    private FileStream? _lease;
    public string Id { get; } = Guid.NewGuid().ToString();
    public SessionMarker? Previous { get; private set; }
    public SessionService(AppDataPaths paths)
    {
        _directory = Path.Combine(paths.Local, "Session");
        _file = new(Path.Combine(_directory, "active-session.json"));
    }
    public async Task StartAsync()
    {
        Directory.CreateDirectory(_directory);
        _lease = new FileStream(Path.Combine(_directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Previous = await _file.ReadAsync();
        // Preserve the previous unclean marker before replacing it. M6 owns recovery resolution.
        if (!Previous.Clean && Previous.Id.Length > 0)
        {
            var archive = new JsonFile<SessionMarker>(Path.Combine(_directory, "unclean-" + Guid.NewGuid().ToString("N") + ".json"));
            await archive.UpdateAsync(_ => Previous);
        }
        await _file.UpdateAsync(_ => new SessionMarker { Id = Id, StartedAtUtc = DateTimeOffset.UtcNow, Clean = false });
    }
    public Task<SessionMarker> CompleteAsync() => _file.UpdateAsync(current => current.Id == Id ? current with { Clean = true } : current);
    public void Dispose() => _lease?.Dispose();
}

public sealed record UpdateStatus(string State, string Message)
{
    public static UpdateStatus NotConfigured { get; } = new("NotConfigured", "Update installation is not configured in this development milestone.");
}
