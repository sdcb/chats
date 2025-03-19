using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Chats.BE.Services.Sessions;

public record SessionEntry
{
    public required int UserId { get; init; }
    public required string UserName { get; init; }
    public required string Role { get; init; }
    public required string? Provider { get; init; }
    public required string? Sub { get; init; }

    public virtual List<Claim> ToClaims()
    {
        List<Claim> claims =
        [
            new Claim(JwtPropertyKeys.UserId, UserId.ToString(), ClaimValueTypes.Integer32),
            new Claim(JwtPropertyKeys.UserName, UserName),
            new Claim(JwtPropertyKeys.Role, Role)
        ];

        if (Provider != null)
        {
            claims.Add(new Claim(JwtPropertyKeys.Provider, Provider));
        }

        if (Sub != null)
        {
            claims.Add(new Claim(JwtPropertyKeys.ProviderSub, Sub));
        }
        return claims;
    }

    public static SessionEntry FromClaims(ClaimsPrincipal claims)
    {
        return new SessionEntry
        {
            UserId = int.Parse(claims.FindFirst(ClaimTypes.NameIdentifier)!.Value),
            UserName = claims.FindFirst(JwtPropertyKeys.UserName)!.Value,
            Role = claims.FindFirst(ClaimTypes.Role)!.Value,
            Provider = claims.FindFirst(JwtPropertyKeys.Provider)?.Value,
            Sub = claims.FindFirst(JwtPropertyKeys.ProviderSub)?.Value
        };
    }
}
