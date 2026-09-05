// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Text.Json;

namespace MeshMe360.Desktop;

internal sealed class StartupLog
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeshMe360", "Logs");
    private readonly string _filename = $"startup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl";

    public async Task WriteAsync(string type, object details)
    {
        // Serialize before yielding so callers may dispose their JsonDocument safely.
        var line = JsonSerializer.Serialize(new { timestampUtc = DateTime.UtcNow, type, details });
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            await File.AppendAllTextAsync(Path.Combine(DirectoryPath, _filename), line + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Mesh Me360 logging failed: {error.Message}");
        }
        finally { _gate.Release(); }
    }
}
