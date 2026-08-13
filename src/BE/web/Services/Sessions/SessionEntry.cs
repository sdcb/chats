using Chats.BE.Services.UrlEncryption;
using System.Security.Claims;

namespace Chats.BE.Services.Sessions;

public record SessionEntry
{
    public required int UserId { get; init; }
    public required string UserName { get; init; }
    public required string Role { get; init; }
    public bool ApiKeyEnabled { get; init; }

    public virtual List<Claim> ToClaims(IUrlEncryptionService idEncryption)
    {
        List<Claim> claims =
        [
            new Claim(JwtPropertyKeys.UserId, idEncryption.EncryptUserId(UserId)),
            new Claim(JwtPropertyKeys.UserName, UserName),
            new Claim(JwtPropertyKeys.Role, Role),
            new Claim(JwtPropertyKeys.ApiKeyEnabled, ApiKeyEnabled.ToString())
        ];
        return claims;
    }

    public static SessionEntry FromClaims(ClaimsPrincipal claims, IUrlEncryptionService idEncryption)
    {
        return new SessionEntry
        {
            UserId = idEncryption.DecryptUserId(claims.FindFirst(ClaimTypes.NameIdentifier)!.Value),
            UserName = claims.FindFirst(JwtPropertyKeys.UserName)!.Value,
            Role = claims.FindFirst(ClaimTypes.Role)!.Value,
            ApiKeyEnabled = bool.TryParse(
                claims.FindFirst(JwtPropertyKeys.ApiKeyEnabled)?.Value,
                out bool apiKeyEnabled) && apiKeyEnabled,
        };
    }
}
