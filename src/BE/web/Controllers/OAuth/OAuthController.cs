using Chats.BE.Infrastructure;
using Chats.BE.Services.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.OAuth;

[ApiController]
public class OAuthController(OAuthAuthorizationService authService) : ControllerBase
{
    [Authorize(AuthenticationSchemes = "SessionId")]
    [HttpGet("/oauth/authorize")]
    public async Task<IActionResult> Authorize([FromQuery] OAuthAuthorizeRequestDto request, [FromServices] CurrentUser currentUser, CancellationToken cancellationToken)
    {
        try
        {
            string redirect = await authService.BuildAuthorizationRedirectAsync(currentUser, new OAuthAuthorizeInput(
                ResponseType: request.ResponseType ?? "code",
                ClientId: request.ClientId ?? string.Empty,
                RedirectUri: request.RedirectUri,
                Scope: request.Scope,
                State: request.State,
                CodeChallenge: request.CodeChallenge,
                CodeChallengeMethod: request.CodeChallengeMethod,
                EncryptedApiKeyId: request.ApiKeyId), cancellationToken);

            return Redirect(redirect);
        }
        catch (OAuthProtocolException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = ex.Error,
                error_description = ex.Description
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("/oauth/token")]
    public async Task<IActionResult> Token([FromForm] OAuthTokenRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            OAuthTokenIssueResult result = request.GrantType switch
            {
                "authorization_code" => await authService.ExchangeAuthorizationCodeAsync(new OAuthTokenInput(
                    GrantType: request.GrantType,
                    ClientId: request.ClientId,
                    ClientSecret: request.ClientSecret,
                    Code: request.Code,
                    RedirectUri: request.RedirectUri,
                    CodeVerifier: request.CodeVerifier,
                    RefreshToken: null), cancellationToken),

                "refresh_token" => await authService.RefreshAccessTokenAsync(new OAuthTokenInput(
                    GrantType: request.GrantType,
                    ClientId: request.ClientId,
                    ClientSecret: request.ClientSecret,
                    Code: null,
                    RedirectUri: null,
                    CodeVerifier: null,
                    RefreshToken: request.RefreshToken), cancellationToken),

                _ => throw new OAuthProtocolException("unsupported_grant_type", "Supported grant_type: authorization_code, refresh_token.")
            };

            return Ok(new
            {
                access_token = result.AccessToken,
                token_type = "Bearer",
                expires_in = result.ExpiresInSeconds,
                refresh_token = result.RefreshToken,
                scope = result.Scope
            });
        }
        catch (OAuthProtocolException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = ex.Error,
                error_description = ex.Description
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("/oauth/revoke")]
    public async Task<IActionResult> Revoke([FromForm] OAuthRevokeRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await authService.RevokeRefreshTokenAsync(new OAuthRevokeInput(
                ClientId: request.ClientId,
                ClientSecret: request.ClientSecret,
                Token: request.Token), cancellationToken);
            return NoContent();
        }
        catch (OAuthProtocolException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = ex.Error,
                error_description = ex.Description
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("/.well-known/oauth-authorization-server")]
    public IActionResult AuthorizationServerMetadata()
    {
        string issuer = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        string authorizationEndpoint = $"{issuer}/oauth/authorize";
        string tokenEndpoint = $"{issuer}/oauth/token";
        string revocationEndpoint = $"{issuer}/oauth/revoke";

        return Ok(new
        {
            issuer,
            authorization_endpoint = authorizationEndpoint,
            token_endpoint = tokenEndpoint,
            revocation_endpoint = revocationEndpoint,
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            token_endpoint_auth_methods_supported = new[] { "none", "client_secret_post" },
            code_challenge_methods_supported = new[] { "S256", "plain" }
        });
    }
}

public record OAuthAuthorizeRequestDto
{
    [FromQuery(Name = "response_type")]
    public string? ResponseType { get; init; }

    [FromQuery(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromQuery(Name = "redirect_uri")]
    public string? RedirectUri { get; init; }

    [FromQuery(Name = "scope")]
    public string? Scope { get; init; }

    [FromQuery(Name = "state")]
    public string? State { get; init; }

    [FromQuery(Name = "code_challenge")]
    public string? CodeChallenge { get; init; }

    [FromQuery(Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; init; }

    [FromQuery(Name = "api_key_id")]
    public string? ApiKeyId { get; init; }
}

public record OAuthTokenRequestDto
{
    [FromForm(Name = "grant_type")]
    public string? GrantType { get; init; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "client_secret")]
    public string? ClientSecret { get; init; }

    [FromForm(Name = "code")]
    public string? Code { get; init; }

    [FromForm(Name = "redirect_uri")]
    public string? RedirectUri { get; init; }

    [FromForm(Name = "code_verifier")]
    public string? CodeVerifier { get; init; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; init; }
}

public record OAuthRevokeRequestDto
{
    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "client_secret")]
    public string? ClientSecret { get; init; }

    [FromForm(Name = "token")]
    public string? Token { get; init; }
}
