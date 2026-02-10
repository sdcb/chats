using Chats.BE.Infrastructure;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.OAuth;

public class OAuthAuthorizationService(
    ChatsDB db,
    IUrlEncryptionService idEncryption,
    OAuthAccessTokenService accessTokenService,
    IConfiguration configuration)
{
    private TimeSpan AuthorizationCodeValidPeriod => TimeSpan.FromMinutes(configuration.GetValue("OAuth:AuthorizationCodeValidMinutes", 5));
    private TimeSpan RefreshTokenValidPeriod => TimeSpan.FromDays(configuration.GetValue("OAuth:RefreshTokenValidDays", 30));

    public async Task<string> BuildAuthorizationRedirectAsync(CurrentUser currentUser, OAuthAuthorizeInput input, CancellationToken cancellationToken)
    {
        if (!string.Equals(input.ResponseType, "code", StringComparison.Ordinal))
        {
            throw new OAuthProtocolException("unsupported_response_type", "Only response_type=code is supported.");
        }

        OAuthClient client = await GetEnabledClient(input.ClientId, cancellationToken);
        string redirectUri = ValidateRedirectUri(client, input.RedirectUri);

        if (client.RequirePkce && string.IsNullOrWhiteSpace(input.CodeChallenge))
        {
            throw new OAuthProtocolException("invalid_request", "code_challenge is required.");
        }

        string codeChallengeMethod = string.IsNullOrWhiteSpace(input.CodeChallengeMethod) ? "S256" : input.CodeChallengeMethod;
        if (client.RequirePkce && !string.Equals(codeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase) && !string.Equals(codeChallengeMethod, "plain", StringComparison.OrdinalIgnoreCase))
        {
            throw new OAuthProtocolException("invalid_request", "Unsupported code_challenge_method.");
        }

        UserApiKey apiKey = await ResolveUserApiKeyAsync(currentUser.Id, input.EncryptedApiKeyId, cancellationToken);
        string code = OAuthCrypto.GenerateOpaqueToken(32);

        OAuthAuthorizationCode authCode = new()
        {
            ClientId = client.Id,
            UserId = currentUser.Id,
            ApiKeyId = apiKey.Id,
            CodeHash = OAuthCrypto.Sha256Base64Url(code),
            RedirectUri = redirectUri,
            CodeChallenge = input.CodeChallenge ?? string.Empty,
            CodeChallengeMethod = codeChallengeMethod,
            Scope = ResolveScope(client.Scope, input.Scope),
            State = input.State,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(AuthorizationCodeValidPeriod),
        };
        db.OAuthAuthorizationCodes.Add(authCode);
        await db.SaveChangesAsync(cancellationToken);

        Dictionary<string, string?> query = new()
        {
            ["code"] = code,
            ["state"] = input.State
        };
        return QueryHelpers.AddQueryString(redirectUri, query);
    }

    public async Task<OAuthTokenIssueResult> ExchangeAuthorizationCodeAsync(OAuthTokenInput input, CancellationToken cancellationToken)
    {
        OAuthClient client = await ValidateTokenClientAsync(input.ClientId, input.ClientSecret, cancellationToken);

        if (string.IsNullOrWhiteSpace(input.Code))
        {
            throw new OAuthProtocolException("invalid_request", "code is required.");
        }

        OAuthAuthorizationCode? authCode = await db.OAuthAuthorizationCodes
            .Include(x => x.User)
            .Include(x => x.ApiKey)
            .FirstOrDefaultAsync(x => x.ClientId == client.Id && x.CodeHash == OAuthCrypto.Sha256Base64Url(input.Code), cancellationToken);

        if (authCode == null || authCode.UsedAt != null || authCode.ExpiresAt <= DateTime.UtcNow)
        {
            throw new OAuthProtocolException("invalid_grant", "Invalid or expired authorization code.");
        }

        if (!string.Equals(authCode.RedirectUri, input.RedirectUri, StringComparison.Ordinal))
        {
            throw new OAuthProtocolException("invalid_grant", "redirect_uri mismatch.");
        }

        if (client.RequirePkce)
        {
            if (string.IsNullOrWhiteSpace(input.CodeVerifier))
            {
                throw new OAuthProtocolException("invalid_request", "code_verifier is required.");
            }

            if (!OAuthCrypto.VerifyPkce(input.CodeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
            {
                throw new OAuthProtocolException("invalid_grant", "PKCE verification failed.");
            }
        }

        EnsureApiKeyIsActive(authCode.ApiKey);

        authCode.UsedAt = DateTime.UtcNow;
        (OAuthRefreshToken refreshToken, string rawRefreshToken) = CreateRefreshTokenEntity(client, authCode.UserId, authCode.ApiKeyId, authCode.Scope);
        db.OAuthRefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(cancellationToken);

        IssuedAccessToken issued = accessTokenService.Issue(authCode.User, authCode.ApiKeyId, client.ClientId, authCode.Scope);
        return new(
            AccessToken: issued.AccessToken,
            ExpiresInSeconds: (int)Math.Max(1, (issued.ExpiresAt - DateTime.UtcNow).TotalSeconds),
            RefreshToken: rawRefreshToken,
            Scope: authCode.Scope);
    }

    public async Task<OAuthTokenIssueResult> RefreshAccessTokenAsync(OAuthTokenInput input, CancellationToken cancellationToken)
    {
        OAuthClient client = await ValidateTokenClientAsync(input.ClientId, input.ClientSecret, cancellationToken);
        if (string.IsNullOrWhiteSpace(input.RefreshToken))
        {
            throw new OAuthProtocolException("invalid_request", "refresh_token is required.");
        }

        string refreshTokenHash = OAuthCrypto.Sha256Base64Url(input.RefreshToken);
        OAuthRefreshToken? refresh = await db.OAuthRefreshTokens
            .Include(x => x.User)
            .Include(x => x.ApiKey)
            .FirstOrDefaultAsync(x => x.ClientId == client.Id && x.TokenHash == refreshTokenHash, cancellationToken);

        if (refresh == null || refresh.RevokedAt != null || refresh.ExpiresAt <= DateTime.UtcNow)
        {
            throw new OAuthProtocolException("invalid_grant", "Invalid or expired refresh token.");
        }

        EnsureApiKeyIsActive(refresh.ApiKey);

        refresh.RevokedAt = DateTime.UtcNow;
        refresh.LastUsedAt = DateTime.UtcNow;

        (OAuthRefreshToken newRefresh, string rawRefreshToken) = CreateRefreshTokenEntity(client, refresh.UserId, refresh.ApiKeyId, refresh.Scope);
        db.OAuthRefreshTokens.Add(newRefresh);
        await db.SaveChangesAsync(cancellationToken);

        IssuedAccessToken issued = accessTokenService.Issue(refresh.User, refresh.ApiKeyId, client.ClientId, refresh.Scope);
        return new(
            AccessToken: issued.AccessToken,
            ExpiresInSeconds: (int)Math.Max(1, (issued.ExpiresAt - DateTime.UtcNow).TotalSeconds),
            RefreshToken: rawRefreshToken,
            Scope: refresh.Scope);
    }

    public async Task RevokeRefreshTokenAsync(OAuthRevokeInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Token))
        {
            return;
        }

        OAuthClient client = await ValidateTokenClientAsync(input.ClientId, input.ClientSecret, cancellationToken, allowUnknownClient: true);
        if (client.Id == 0)
        {
            return;
        }

        string hash = OAuthCrypto.Sha256Base64Url(input.Token);
        OAuthRefreshToken? refresh = await db.OAuthRefreshTokens
            .FirstOrDefaultAsync(x => x.ClientId == client.Id && x.TokenHash == hash, cancellationToken);
        if (refresh == null || refresh.RevokedAt != null)
        {
            return;
        }

        refresh.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureApiKeyIsActive(UserApiKey apiKey)
    {
        if (apiKey.IsDeleted || apiKey.IsRevoked || apiKey.Expires <= DateTime.UtcNow)
        {
            throw new OAuthProtocolException("invalid_grant", "Mapped api key is unavailable.");
        }
    }

    private (OAuthRefreshToken Entity, string RawToken) CreateRefreshTokenEntity(OAuthClient client, int userId, int apiKeyId, string? scope)
    {
        string rawToken = OAuthCrypto.GenerateOpaqueToken(48);
        OAuthRefreshToken entity = new()
        {
            ClientId = client.Id,
            UserId = userId,
            ApiKeyId = apiKeyId,
            TokenHash = OAuthCrypto.Sha256Base64Url(rawToken),
            Scope = scope,
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenValidPeriod),
            CreatedAt = DateTime.UtcNow,
        };
        return (entity, rawToken);
    }

    private async Task<UserApiKey> ResolveUserApiKeyAsync(int userId, string? encryptedApiKeyId, CancellationToken cancellationToken)
    {
        IQueryable<UserApiKey> query = db.UserApiKeys
            .Where(x => x.UserId == userId && !x.IsDeleted && !x.IsRevoked && x.Expires > DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(encryptedApiKeyId))
        {
            int decrypted = idEncryption.DecryptApiKeyId(encryptedApiKeyId);
            UserApiKey? selected = await query.FirstOrDefaultAsync(x => x.Id == decrypted, cancellationToken);
            return selected ?? throw new OAuthProtocolException("invalid_request", "api_key_id not found.");
        }

        UserApiKey? defaultKey = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultKey ?? throw new OAuthProtocolException("invalid_request", "No active api key is available for current user.");
    }

    private async Task<OAuthClient> GetEnabledClient(string clientId, CancellationToken cancellationToken)
    {
        OAuthClient? client = await db.OAuthClients.FirstOrDefaultAsync(x => x.ClientId == clientId && x.IsEnabled, cancellationToken);
        return client ?? throw new OAuthProtocolException("invalid_client", "Client is invalid or disabled.", 401);
    }

    private async Task<OAuthClient> ValidateTokenClientAsync(string? clientId, string? clientSecret, CancellationToken cancellationToken, bool allowUnknownClient = false)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new OAuthProtocolException("invalid_client", "client_id is required.", 401);
        }

        OAuthClient? client = await db.OAuthClients.FirstOrDefaultAsync(x => x.ClientId == clientId && x.IsEnabled, cancellationToken);
        if (client == null)
        {
            if (allowUnknownClient)
            {
                return new OAuthClient { Id = 0, ClientId = clientId, Name = string.Empty, RedirectUris = string.Empty };
            }
            throw new OAuthProtocolException("invalid_client", "Client is invalid or disabled.", 401);
        }

        if (client.RequireClientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientSecret) || !OAuthCrypto.VerifyClientSecret(client.ClientSecretHash, clientSecret))
            {
                throw new OAuthProtocolException("invalid_client", "Client secret is invalid.", 401);
            }
        }

        return client;
    }

    private static string ValidateRedirectUri(OAuthClient client, string? redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new OAuthProtocolException("invalid_request", "redirect_uri is required.");
        }

        HashSet<string> allowedUris = ParseRedirectUris(client.RedirectUris);
        if (!allowedUris.Contains(redirectUri))
        {
            throw new OAuthProtocolException("invalid_request", "redirect_uri is not allowed for this client.");
        }

        return redirectUri;
    }

    private static HashSet<string> ParseRedirectUris(string redirectUris)
    {
        return redirectUris
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? ResolveScope(string? clientScope, string? requestedScope)
    {
        if (!string.IsNullOrWhiteSpace(requestedScope))
        {
            return requestedScope.Trim();
        }

        return string.IsNullOrWhiteSpace(clientScope) ? null : clientScope.Trim();
    }
}

public record OAuthAuthorizeInput(
    string ResponseType,
    string ClientId,
    string? RedirectUri,
    string? Scope,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? EncryptedApiKeyId);

public record OAuthTokenInput(
    string? GrantType,
    string? ClientId,
    string? ClientSecret,
    string? Code,
    string? RedirectUri,
    string? CodeVerifier,
    string? RefreshToken);

public record OAuthRevokeInput(string? ClientId, string? ClientSecret, string? Token);

public record OAuthTokenIssueResult(string AccessToken, int ExpiresInSeconds, string RefreshToken, string? Scope);
