using System.Security.Cryptography;
using System.Text;

namespace TaxNavi.Application.Services;

/// <summary>Bearer-style access tokens for guest reports. Only the SHA-256 hash is stored.</summary>
public static class ReportAccessTokens
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool Matches(string? token, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrEmpty(storedHash) || token.Length > 128)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(storedHash));
    }
}
