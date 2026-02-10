using System.Security.Cryptography;
using System.Text;

namespace Chats.BE.Services.OAuth;

public static class OAuthCrypto
{
    public static string GenerateOpaqueToken(int byteLength = 32)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Sha256Base64Url(string text)
    {
        byte[] input = Encoding.UTF8.GetBytes(text);
        byte[] hash = SHA256.HashData(input);
        return Base64UrlEncode(hash);
    }

    public static bool VerifyPkce(string codeVerifier, string codeChallenge, string codeChallengeMethod)
    {
        if (string.Equals(codeChallengeMethod, "plain", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(codeVerifier, codeChallenge, StringComparison.Ordinal);
        }

        // OAuth2 PKCE default we support is S256.
        if (!string.Equals(codeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string computed = Sha256Base64Url(codeVerifier);
        return string.Equals(computed, codeChallenge, StringComparison.Ordinal);
    }

    public static string HashClientSecret(string secret)
    {
        return Sha256Base64Url(secret);
    }

    public static bool VerifyClientSecret(string? secretHash, string providedSecret)
    {
        if (string.IsNullOrWhiteSpace(secretHash))
        {
            return false;
        }

        string providedHash = HashClientSecret(providedSecret);
        return FixedTimeEquals(secretHash, providedHash);
    }

    public static bool FixedTimeEquals(string a, string b)
    {
        byte[] bytesA = Encoding.UTF8.GetBytes(a);
        byte[] bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
