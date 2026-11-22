using System.Security.Cryptography;
using System.Text;

namespace Chats.BE.Services.WebsiteHosting;

public static class WebsiteHostingHttpClientNames
{
    public const string CaddyAdmin = "WebsiteHosting.CaddyAdmin";
    public const string Cloudflare = "WebsiteHosting.Cloudflare";
    public const string AliyunDns = "WebsiteHosting.AliyunDns";

    public static readonly string[] All = [CaddyAdmin, Cloudflare, AliyunDns];
}

public static class WebsiteHostingErrorCodes
{
    public const string InvalidSubdomain = "InvalidSubdomain";
    public const string DomainNotFound = "DomainNotFound";
    public const string DomainNotAllowed = "DomainNotAllowed";
    public const string ContainerNotFound = "ContainerNotFound";
    public const string PermanentContainerRequired = "PermanentContainerRequired";
    public const string InvalidPort = "InvalidPort";
    public const string QuotaExceeded = "QuotaExceeded";
    public const string HostnameTaken = "HostnameTaken";
    public const string DnsConflict = "DnsConflict";
    public const string DnsError = "DnsError";
    public const string GatewayError = "GatewayError";
    public const string GatewayNotConfigured = "GatewayNotConfigured";
    public const string PortMappingFailed = "PortMappingFailed";
    public const string SiteNotFound = "SiteNotFound";
    public const string InvalidState = "InvalidState";
    public const string InvalidConfiguration = "InvalidConfiguration";
}

public sealed class WebsiteHostingException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}

public sealed class WebsiteHostingOptions
{
    public bool ReconcileEnabled { get; set; } = true;
    public int ReconcileIntervalSeconds { get; set; } = 300;
    public int ReconcileInitialDelaySeconds { get; set; } = 30;
}

/// <summary>
/// Encrypts gateway / DNS provider credentials at rest with AES-GCM. The key is derived from
/// the ENCRYPTION_PASSWORD setting (the same secret used for URL id encryption), so changing that
/// setting requires re-entering the credentials. Values without the "enc:v1:" prefix are treated
/// as plaintext (legacy/manual rows) and returned unchanged.
/// </summary>
public sealed class WebsiteCredentialProtector
{
    public const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public WebsiteCredentialProtector(IConfiguration configuration, ILogger<WebsiteCredentialProtector> logger)
        : this(ResolvePassword(configuration, logger))
    {
    }

    public WebsiteCredentialProtector(string password)
    {
        _key = Rfc2898DeriveBytes.Pbkdf2(password, Encoding.UTF8.GetBytes("chats-website-credential"), 10000, HashAlgorithmName.SHA256, 32);
    }

    private static string ResolvePassword(IConfiguration configuration, ILogger logger)
    {
        string? password = configuration["ENCRYPTION_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("ENCRYPTION_PASSWORD is not set; website hosting credentials are encrypted with a built-in key.");
            return "chats-default-credential-key";
        }
        return password;
    }

    public string Protect(string plaintext)
    {
        byte[] plain = Encoding.UTF8.GetBytes(plaintext);
        byte[] output = new byte[NonceSize + TagSize + plain.Length];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        Span<byte> tag = output.AsSpan(NonceSize, TagSize);
        Span<byte> cipher = output.AsSpan(NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);
        using AesGcm aes = new(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Prefix + Convert.ToBase64String(output);
    }

    public string? ProtectNullable(string? plaintext) => string.IsNullOrEmpty(plaintext) ? null : Protect(plaintext);

    public string Unprotect(string stored)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;
        byte[] input = Convert.FromBase64String(stored[Prefix.Length..]);
        if (input.Length < NonceSize + TagSize) throw new CryptographicException("Invalid protected credential.");
        byte[] plain = new byte[input.Length - NonceSize - TagSize];
        using AesGcm aes = new(_key, TagSize);
        aes.Decrypt(input.AsSpan(0, NonceSize), input.AsSpan(NonceSize + TagSize), input.AsSpan(NonceSize, TagSize), plain);
        return Encoding.UTF8.GetString(plain);
    }

    public string? UnprotectNullable(string? stored) => string.IsNullOrEmpty(stored) ? null : Unprotect(stored);
}
