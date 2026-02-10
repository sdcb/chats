using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Services;
using Chats.DB;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Chats.BE.Services.OAuth;

public class OAuthAccessTokenService(JwtKeyManager jwtKeyManager, IConfiguration configuration, IUrlEncryptionService idEncryption)
{
    private string Issuer => configuration.GetValue<string>("OAuth:Issuer") ?? "chats-oauth";
    private string Audience => configuration.GetValue<string>("OAuth:Audience") ?? "chats-api";
    private TimeSpan AccessTokenValidPeriod => TimeSpan.FromMinutes(configuration.GetValue("OAuth:AccessTokenValidMinutes", 30));

    public IssuedAccessToken Issue(User user, int apiKeyId, string clientId, string? scope)
    {
        DateTime expiresAt = DateTime.UtcNow.Add(AccessTokenValidPeriod);

        List<Claim> claims =
        [
            new(JwtPropertyKeys.UserId, idEncryption.EncryptUserId(user.Id)),
            new(JwtPropertyKeys.UserName, user.DisplayName),
            new(JwtPropertyKeys.Role, user.Role),
            new("api-key-id", apiKeyId.ToString()),
            new("client_id", clientId),
        ];
        if (!string.IsNullOrWhiteSpace(scope))
        {
            claims.Add(new Claim("scope", scope));
        }

        SigningCredentials credentials = new(GetSecurityKey(), SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new(accessToken, expiresAt);
    }

    public ClaimsPrincipal Validate(string token)
    {
        TokenValidationParameters validationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSecurityKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
        };

        JwtSecurityTokenHandler handler = new()
        {
            MapInboundClaims = false,
        };
        return handler.ValidateToken(token, validationParameters, out _);
    }

    private SymmetricSecurityKey GetSecurityKey() => new(Pdkdf2StringToByte32(jwtKeyManager.GetOrCreateSecretKey()));

    private static byte[] Pdkdf2StringToByte32(string input)
    {
        byte[] salt = new byte[16];
        return Rfc2898DeriveBytes.Pbkdf2(input, salt, 10000, HashAlgorithmName.SHA256, 32);
    }
}

public record IssuedAccessToken(string AccessToken, DateTime ExpiresAt);
