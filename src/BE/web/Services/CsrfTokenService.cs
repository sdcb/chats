using System.Security.Cryptography;

namespace Chats.Web.Services;

public class CsrfTokenService
{
    private readonly byte[] _key;
    private readonly ILogger<CsrfTokenService> _logger;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan TokenValidity = TimeSpan.FromMinutes(5);

    public CsrfTokenService(IConfiguration configuration, ILogger<CsrfTokenService> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        string? signingKey = configuration["SigningKey"];

        if (string.IsNullOrEmpty(signingKey))
        {
            _key = new byte[32];
            RandomNumberGenerator.Fill(_key);
            string base64Key = Convert.ToBase64String(_key);
            _logger.LogWarning("No SigningKey found in configuration. Generated a new one: {SigningKey}", base64Key);
        }
        else
        {
            _key = Convert.FromBase64String(signingKey);
            if (_key.Length != 32)
            {
                throw new ArgumentException("SigningKey must be 32 bytes long");
            }
        }
    }

    public string GenerateToken()
    {
        using HMACSHA256 hmac = new (_key);
        long timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        byte[] timestampBytes = BitConverter.GetBytes(timestamp);
        byte[] tokenBytes = hmac.ComputeHash(timestampBytes);
        byte[] combined = new byte[timestampBytes.Length + tokenBytes.Length];
        Buffer.BlockCopy(timestampBytes, 0, combined, 0, timestampBytes.Length);
        Buffer.BlockCopy(tokenBytes, 0, combined, timestampBytes.Length, tokenBytes.Length);
        return Convert.ToBase64String(combined);
    }

    public bool VerifyToken(string token)
    {
        byte[] tokenBytes;
        try
        {
            tokenBytes = Convert.FromBase64String(token);
        }
        catch (FormatException)
        {
            return false;
        }

        if (tokenBytes.Length != 40)
        {
            return false;
        }

        byte[] timestampBytes = new byte[8];
        byte[] hashBytes = new byte[32];
        Buffer.BlockCopy(tokenBytes, 0, timestampBytes, 0, 8);
        Buffer.BlockCopy(tokenBytes, 8, hashBytes, 0, 32);

        long timestamp = BitConverter.ToInt64(timestampBytes, 0);
        long currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        if (currentTime - timestamp > TokenValidity.TotalSeconds)
        {
            return false;
        }

        using HMACSHA256 hmac = new(_key);
        byte[] expectedHashBytes = hmac.ComputeHash(timestampBytes);
        return expectedHashBytes.SequenceEqual(hashBytes);
    }
}