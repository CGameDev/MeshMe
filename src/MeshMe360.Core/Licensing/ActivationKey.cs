// SPDX-License-Identifier: AGPL-3.0-only
namespace MeshMe360.Core.Licensing;

public static class ActivationKey
{
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    public static string Normalize(string value) => string.Concat(value.Where(c => c != '-' && !char.IsWhiteSpace(c))).ToUpperInvariant();
    public static bool IsValid(string value)
    {
        var key = Normalize(value);
        if (key.Length != 25 || key.Any(c => !Alphabet.Contains(c))) return false;
        var checksum = 0;
        foreach (var c in key[..24]) checksum = (checksum * 17 + Alphabet.IndexOf(c)) % 32;
        return key[24] == Alphabet[checksum];
    }
}
