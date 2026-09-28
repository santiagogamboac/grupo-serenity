using System.Security.Cryptography;
using System.Text;

namespace Serenity.Application.Common;

/// <summary>SHA-256 hex de un token opaco: en la BD solo se guarda el hash, nunca el token.</summary>
public static class TokenHasher
{
    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
