using System;
using System.Security.Cryptography;
using System.Text;

namespace NextLearn.Desktop.Services;

public static class DeckFileIdentity
{
    public static Guid GetId(string filePath)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }
}
