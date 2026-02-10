using Chats.BE.Services.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using Chats.DB;

namespace Chats.BE.Infrastructure;

public class OAuthAccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    OAuthAccessTokenService tokenService,
    ChatsDB db,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string authorizationHeaderString = authorizationHeader.ToString();
        if (!authorizationHeaderString.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string accessToken = authorizationHeaderString["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            System.Security.Claims.ClaimsPrincipal principal = tokenService.Validate(accessToken);
            string? apiKeyIdString = principal.FindFirst("api-key-id")?.Value;
            if (!int.TryParse(apiKeyIdString, out int apiKeyId))
            {
                return AuthenticateResult.Fail("api-key-id claim is missing.");
            }

            bool apiKeyValid = await db.UserApiKeys
                .AnyAsync(x => x.Id == apiKeyId && !x.IsDeleted && !x.IsRevoked && x.Expires > DateTime.UtcNow);
            if (!apiKeyValid)
            {
                return AuthenticateResult.Fail("Mapped api key is invalid.");
            }

            AuthenticationTicket ticket = new(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex);
        }
    }
}
