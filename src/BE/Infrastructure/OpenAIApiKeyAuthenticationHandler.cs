using Chats.BE.Services.Common;
using Chats.BE.Services.OpenAIApiKeySession;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Chats.BE.Infrastructure;

public class OpenAIApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    OpenAIApiKeySessionManager sessionManager,
    UrlEncoder encoder, 
    IUrlEncryptionService idEncryption) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string authorizationHeaderString = authorizationHeader.ToString();
        string apiKey = authorizationHeaderString.Split(' ').Last();

        ApiKeyEntry? apiKeyInfo = await sessionManager.GetCachedUserInfoByOpenAIApiKey(apiKey);

        if (apiKeyInfo == null)
        {
            return AuthenticateResult.Fail($"Invalid API Key: {apiKey}");
        }

        if (apiKeyInfo.Expires.IsExpired())
        {
            return AuthenticateResult.Fail($"API Key expired: {apiKey}");
        }

        ClaimsIdentity identity = new(apiKeyInfo.ToClaims(idEncryption), Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}