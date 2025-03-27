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
        if (!Request.Headers.TryGetValue("Authorization", out StringValues authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string authorizationHeaderString = authorizationHeader.ToString();
        string[] segments = authorizationHeaderString.Split(' ');
        if (segments.Length != 2 || segments[0] != "Bearer")
        {
            return AuthenticateResult.Fail("Invalid authorization header");
        }

        string jwt = segments[1];
        try
        {
            SessionEntry userInfo = await sessionManager.GetCachedUserInfoBySession(jwt);
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
}