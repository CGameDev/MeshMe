// SPDX-License-Identifier: AGPL-3.0-only
// Validates native file selections and issues single-use binary stream grants.
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace MeshMe360.Core;

public sealed record StartupFile(string? Path, string? Warning);
public static class HostFiles
{
    public static readonly string[] Extensions = [".stl", ".obj", ".3mf", ".step", ".stp", ".mm360", ".bumpmesh"];
    /// <summary>Resolve and validate a supported existing path before granting access.</summary>
    public static string Validate(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (!Extensions.Contains(System.IO.Path.GetExtension(full), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Supported files: STL, OBJ, 3MF, STEP/STP, .mm360 and legacy .bumpmesh.");
        if (!File.Exists(full)) throw new FileNotFoundException("The selected file no longer exists.", full);
        return full;
    }
    /// <summary>Choose the first supported startup document and report extra arguments.</summary>
    public static StartupFile ParseArguments(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return new(null, null);
        var warning = args.Count > 1 ? "Mesh Me360 supports one document at a time; opening the first supported file." : null;
        var first = args.FirstOrDefault(p => Extensions.Contains(System.IO.Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));
        if (first is null) throw new InvalidDataException("No supported model or project was supplied.");
        return new(Validate(first), warning);
    }
}

public sealed record FileGrant(string Token, string Path, string Filename, string RequestId, DateTimeOffset ExpiresAt);
public sealed class FileTokenStore : IDisposable
{
    private readonly Dictionary<string, FileGrant> _tokens = new(StringComparer.Ordinal);
    private readonly List<Stream> _streams = [];
    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _now;
    public FileTokenStore(Func<DateTimeOffset>? now = null) => _now = now ?? (() => DateTimeOffset.UtcNow);
    /// <summary>Create a short-lived opaque grant without disclosing the native path to the renderer.</summary>
    public FileGrant Issue(string path, string requestId)
    {
        var full = HostFiles.Validate(path);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var grant = new FileGrant(token, full, System.IO.Path.GetFileName(full), requestId, _now().AddMinutes(5));
        lock (_gate) { _tokens.Add(token, grant); }
        return grant;
    }
    /// <summary>Validate request method and origin, then consume the grant exactly once.</summary>
    public (Stream Stream, FileGrant Grant) Consume(string method, string rawUri, string origin)
    {
        if (method != "GET" || origin != HostOrigins.App)
            throw new UnauthorizedAccessException("Unsupported bridge method or origin.");
        var match = Regex.Match(rawUri, @"\Ahttps://bridge\.meshme360\.local/file/([a-f0-9]{64})\z", RegexOptions.CultureInvariant);
        if (!match.Success) throw new UnauthorizedAccessException("Invalid bridge route.");
        lock (_gate)
        {
            if (!_tokens.Remove(match.Groups[1].Value, out var grant) || grant.ExpiresAt <= _now())
                throw new UnauthorizedAccessException("Unknown, consumed or expired token.");
            // Only a stream is created. No ReadAllBytes, base64, or large host buffer.
            var stream = new FileStream(grant.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            _streams.Add(stream); // Keep alive until document replacement/navigation/session disposal.
            return (stream, grant);
        }
    }
    /// <summary>Revoke pending grants and close streams when the document session ends.</summary>
    public void RevokeAll()
    {
        lock (_gate)
        {
            _tokens.Clear();
            foreach (var stream in _streams) stream.Dispose();
            _streams.Clear();
        }
    }
    public void Dispose() => RevokeAll();
}

public sealed record SaveOperation(string Extension, string Label, string Filter)
{
    public static SaveOperation FromFilename(string filename) => System.IO.Path.GetExtension(filename).ToLowerInvariant() switch
    {
        ".mm360" => new(".mm360", "Save Project", "Mesh Me360 project (*.mm360)|*.mm360"),
        ".obj" => new(".obj", "Export OBJ", "Wavefront OBJ (*.obj)|*.obj"),
        ".zip" when filename.EndsWith(".obj.zip", StringComparison.OrdinalIgnoreCase) => new(".zip", "Export OBJ package", "OBJ package (*.obj.zip)|*.obj.zip"),
        ".bumpmesh" => new(".bumpmesh", "Save Project", "Mesh Me360 project (*.bumpmesh)|*.bumpmesh"),
        ".stl" => new(".stl", "Export STL", "Binary STL (*.stl)|*.stl"),
        ".3mf" => new(".3mf", "Export 3MF", "3MF (*.3mf)|*.3mf"),
        _ => throw new InvalidDataException("Unsupported download format.")
    };
}
