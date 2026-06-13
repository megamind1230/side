using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NextLearn.Desktop.Services;

public static class DeckFileIdentity
{
    public static Guid GetId(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? "";
        var name = Path.GetFileName(filePath);
        // Strip leading '+' (pin) and trailing '~' (archive) for stable IDs
        if (name.StartsWith('+')) name = name[1..];
        if (name.EndsWith('~')) name = name[..^1];
        var cleanPath = Path.Combine(dir, name);

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(cleanPath));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }
}
