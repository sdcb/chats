using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Chats.BE.Infrastructure;

public class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    SessionManager sessionManager,
    UrlEncoder encoder, 
    IUrlEncryptionService idEncryption) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? jwt = null;

        if (Request.Headers.TryGetValue("Authorization", out StringValues authHeader))
        {
            string authHeaderString = authHeader.ToString();
            if (authHeaderString.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                jwt = authHeaderString["Bearer ".Length..].Trim();
            }
            else
            {
                return AuthenticateResult.NoResult();
            }
        }

        if (string.IsNullOrWhiteSpace(jwt))
        {
            if (Request.Query.TryGetValue("token", out StringValues tokenQuery))
            {
                jwt = tokenQuery.ToString();
            }
        }

        if (string.IsNullOrWhiteSpace(jwt))
        {
            return AuthenticateResult.NoResult();
        }

        if (CountJwtTokenPart(jwt, 3) != 3)
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            SessionEntry userInfo = await sessionManager.GetUserInfoByJwt(jwt);
            ClaimsIdentity identity = new(userInfo.ToClaims(idEncryption), Scheme.Name, JwtPropertyKeys.UserId, JwtPropertyKeys.Role);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex);
        }
    }

    internal static int CountJwtTokenPart(string token, int maxCount)
    {
        var count = 1;
        var index = 0;
        while (index < token.Length)
        {
            var dotIndex = token.IndexOf('.', index);
            if (dotIndex < 0)
            {
                break;
            }
            count++;
            index = dotIndex + 1;
            if (count == maxCount)
            {
                break;
            }
        }
        return count;
    }
}
