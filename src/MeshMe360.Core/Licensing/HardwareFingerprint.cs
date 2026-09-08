// SPDX-License-Identifier: AGPL-3.0-only
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MeshMe360.Core.Licensing;

public interface IHardwareFingerprintService { Task<HardwareFingerprint> GetAsync(); }

public sealed record HardwareFingerprint(int Version, Dictionary<string, string> Components, string Combined)
{
    public static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>
        { ["systemUuid"] = 50, ["baseboard"] = 25, ["bios"] = 15, ["machineGuid"] = 10 };
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static HardwareFingerprint Create(IReadOnlyDictionary<string, string?> raw)
    {
        var components = new Dictionary<string, string>();
        foreach (var name in Weights.Keys)
        {
            var value = raw.GetValueOrDefault(name)?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(value)) continue;
            var compact = Regex.Replace(value, "[^A-Z0-9]", "");
            if (compact.Length < 4 || compact.All(c => c == '0') || compact.All(c => c == 'F') ||
                new[] { "TOBEFILLEDBYOEM", "DEFAULTSTRING", "SYSTEMSERIALNUMBER", "UNKNOWN", "NONE", "NOTAPPLICABLE", "NOTSPECIFIED", "123456789", "1234567890" }.Contains(compact)) continue;
            if (name is "systemUuid" or "machineGuid")
            {
                if (!Guid.TryParse(value, out var id)) continue;
                value = id.ToString("D").ToUpperInvariant();
            }
            components[name] = Hash($"meshme360:hardware:v1:{name}:{value}");
        }
        var combined = Hash(string.Join("\n", components.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value)));
        return new(1, components, combined);
    }
    public void Validate()
    {
        if (Version != 1 || Components.Count == 0 || Components.Any(p => !Weights.ContainsKey(p.Key) || !Regex.IsMatch(p.Value, "^[0-9a-f]{64}$")) ||
            Combined != Hash(string.Join("\n", Components.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value))))
            throw new InvalidDataException("Invalid hardware fingerprint.");
    }
    public bool Matches(HardwareFingerprint other)
    {
        Validate(); other.Validate();
        // A conflicting strong component is a replacement, even when weaker IDs were copied.
        foreach (var name in new[] { "systemUuid", "baseboard" })
            if (Components.TryGetValue(name, out var a) && other.Components.TryGetValue(name, out var b) && a != b) return false;
        var score = Weights.Where(p => Components.TryGetValue(p.Key, out var a) && other.Components.TryGetValue(p.Key, out var b) && a == b).Sum(p => p.Value);
        return score >= 60;
    }
}
