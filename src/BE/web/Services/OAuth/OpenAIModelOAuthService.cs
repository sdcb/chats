using Chats.DB;
using Chats.DB.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.OAuth;

public class OpenAIModelOAuthService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<OpenAIModelOAuthService> logger)
{
    private sealed record PendingState(short ModelKeyId, string CodeVerifier, string RedirectUri, DateTime ExpiresAtUtc);

    private const string DefaultIssuer = "https://auth.openai.com";
    private const string DefaultClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    private const string DefaultScope = "openid profile email offline_access";
    private const string DefaultOriginator = "codex_cli_rs";
    private const string HardcodedOpenAITokenEndpoint = "https://auth.openai.com/oauth/token";

    private static readonly ConcurrentDictionary<string, PendingState> _pendingStates = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<short, SemaphoreSlim> _modelLocks = new();

    private const string RefreshScope = "openid profile email";

    public async Task<OpenAIModelOAuthStartResult> StartAuthorizationAsync(short modelKeyId, string callbackUrl, CancellationToken cancellationToken, bool allowApiKeySource = false)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        ModelKey? modelKey = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            throw new OAuthProtocolException("invalid_request", $"Model key {modelKeyId} not found.");
        }

        if (modelKey.AuthType != DBModelAuthType.OAuth && !allowApiKeySource)
        {
            throw new OAuthProtocolException("invalid_request", "Model key authType must be OAuth.");
        }

        OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(modelKey.OAuthConfigJson);
        string issuer = ResolveIssuer(config);
        string clientId = ResolveClientId(config);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new OAuthProtocolException("invalid_request", "OAuth clientId is required.");
        }

        string codeVerifier = OAuthCrypto.GenerateOpaqueToken(64);
        string codeChallenge = OAuthCrypto.Sha256Base64Url(codeVerifier);
        string state = OAuthCrypto.GenerateOpaqueToken(32);

        string redirectUri = callbackUrl;
        string authorizationEndpoint = ResolveAuthorizationEndpoint(config, issuer);
        string scopeValue = string.IsNullOrWhiteSpace(config.Scope) ? DefaultScope : config.Scope!;
        string? allowedWorkspaceId = ResolveAllowedWorkspaceId(config);
        bool useCodexDefaults = string.Equals(clientId, DefaultClientId, StringComparison.Ordinal);
        string? originator = !string.IsNullOrWhiteSpace(config.Originator)
            ? config.Originator
            : useCodexDefaults ? DefaultOriginator : null;
        bool idTokenAddOrganizations = config.IdTokenAddOrganizations ?? useCodexDefaults;
        bool codexCliSimplifiedFlow = config.CodexCliSimplifiedFlow ?? useCodexDefaults;

        _pendingStates[state] = new PendingState(modelKeyId, codeVerifier, redirectUri, DateTime.UtcNow.AddMinutes(10));

        Dictionary<string, string?> query = new()
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scopeValue,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };
        if (config.IdTokenAddOrganizations.HasValue || useCodexDefaults)
        {
            query["id_token_add_organizations"] = idTokenAddOrganizations ? "true" : "false";
        }
        if (config.CodexCliSimplifiedFlow.HasValue || useCodexDefaults)
        {
            query["codex_cli_simplified_flow"] = codexCliSimplifiedFlow ? "true" : "false";
        }
        if (!string.IsNullOrWhiteSpace(originator))
        {
            query["originator"] = originator;
        }
        if (!string.IsNullOrWhiteSpace(allowedWorkspaceId))
        {
            query["allowed_workspace_id"] = allowedWorkspaceId;
        }
        string authorizationUrl = QueryHelpers.AddQueryString(authorizationEndpoint, query);

        return new OpenAIModelOAuthStartResult
        {
            AuthorizationUrl = authorizationUrl,
            State = state,
            RedirectUri = redirectUri,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        };
    }

    public async Task<OpenAIModelOAuthCallbackResult> HandleCallbackAsync(string? state, string? code, string? error, string? errorDescription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state) || !_pendingStates.TryRemove(state, out PendingState? pending))
        {
            throw new OAuthProtocolException("invalid_request", "Invalid or expired oauth state.");
        }
        if (pending.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new OAuthProtocolException("invalid_request", "OAuth state is expired.");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            await SaveLastErrorAsync(pending.ModelKeyId, $"{error}: {errorDescription}", cancellationToken);
            return new OpenAIModelOAuthCallbackResult
            {
                Success = false,
                ModelKeyId = pending.ModelKeyId,
                Error = error,
                ErrorDescription = errorDescription,
            };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new OAuthProtocolException("invalid_request", "Missing authorization code.");
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        ModelKey? modelKey = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == pending.ModelKeyId, cancellationToken);
        if (modelKey == null)
        {
            throw new OAuthProtocolException("invalid_request", $"Model key {pending.ModelKeyId} not found.");
        }

        OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(modelKey.OAuthConfigJson);
        string tokenEndpoint = ResolveTokenEndpoint(config, ResolveIssuer(config));
        string clientId = ResolveClientId(config);

        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = pending.RedirectUri,
            ["code_verifier"] = pending.CodeVerifier,
        };
        if (!string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            form["client_secret"] = config.ClientSecret;
        }

        OAuthTokenEndpointResponse tokenResult = await RequestTokenAsync(tokenEndpoint, form, cancellationToken);
        DateTime expiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn ?? 3600);
        OpenAIModelOAuthConfig updated = config with
        {
            RedirectUri = pending.RedirectUri,
            AccessToken = tokenResult.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(tokenResult.RefreshToken) ? config.RefreshToken : tokenResult.RefreshToken,
            IdToken = tokenResult.IdToken,
            AllowedWorkspaceId = string.IsNullOrWhiteSpace(config.AllowedWorkspaceId)
                ? ExtractFirstOrganizationId(tokenResult.IdToken)
                : config.AllowedWorkspaceId,
            ApiKeyAccessToken = null,
            AccessTokenExpiresAtUtc = expiresAt.ToString("O"),
            LastError = null,
            UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
        };
        if (modelKey.AuthType != DBModelAuthType.OAuth)
        {
            modelKey.AuthType = DBModelAuthType.OAuth;
        }
        modelKey.OAuthConfigJson = updated.ToJson();
        await db.SaveChangesAsync(cancellationToken);

        return new OpenAIModelOAuthCallbackResult
        {
            Success = true,
            ModelKeyId = pending.ModelKeyId,
        };
    }

    public async Task<string> GetAccessTokenForModelKeyAsync(ModelKey modelKey, CancellationToken cancellationToken)
    {
        if (modelKey.AuthType != DBModelAuthType.OAuth)
        {
            return modelKey.Secret ?? throw new OAuthProtocolException("invalid_request", "Model secret is empty.");
        }

        SemaphoreSlim modelLock = _modelLocks.GetOrAdd(modelKey.Id, _ => new SemaphoreSlim(1, 1));
        await modelLock.WaitAsync(cancellationToken);
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            ModelKey? latest = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == modelKey.Id, cancellationToken);
            if (latest == null)
            {
                throw new OAuthProtocolException("invalid_request", $"Model key {modelKey.Id} not found.");
            }

            OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(latest.OAuthConfigJson);
            DateTime? expiresAt = config.ParseAccessTokenExpiresAtUtc();
            if (!string.IsNullOrWhiteSpace(config.AccessToken) && expiresAt != null && expiresAt > DateTime.UtcNow.AddSeconds(30))
            {
                return config.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(config.RefreshToken))
            {
                if (!string.IsNullOrWhiteSpace(config.AccessToken))
                {
                    return config.AccessToken;
                }

                if (!string.IsNullOrWhiteSpace(latest.Secret))
                {
                    return latest.Secret;
                }

                throw new OAuthProtocolException("invalid_grant", "OAuth token is unavailable and refresh token is missing.");
            }

            string tokenEndpoint = ResolveTokenEndpoint(config, ResolveIssuer(config));
            string clientId = ResolveClientId(config);
            OAuthTokenEndpointResponse refreshed = await RefreshAccessTokenAsync(
                tokenEndpoint,
                clientId,
                config.ClientSecret,
                config.RefreshToken,
                cancellationToken);
            DateTime refreshedExpireAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn ?? 3600);
            OpenAIModelOAuthConfig updated = config with
            {
                AccessToken = refreshed.AccessToken,
                RefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken) ? config.RefreshToken : refreshed.RefreshToken,
                IdToken = string.IsNullOrWhiteSpace(refreshed.IdToken) ? config.IdToken : refreshed.IdToken,
                AllowedWorkspaceId = string.IsNullOrWhiteSpace(config.AllowedWorkspaceId)
                    ? ExtractFirstOrganizationId(string.IsNullOrWhiteSpace(refreshed.IdToken) ? config.IdToken : refreshed.IdToken)
                    : config.AllowedWorkspaceId,
                ApiKeyAccessToken = null,
                AccessTokenExpiresAtUtc = refreshedExpireAt.ToString("O"),
                LastError = null,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
            };
            latest.OAuthConfigJson = updated.ToJson();
            await db.SaveChangesAsync(cancellationToken);

            return refreshed.AccessToken;
        }
        catch (OAuthProtocolException ex) when (ContainsRefreshTokenReusedError(ex.Description))
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            ModelKey? latest = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == modelKey.Id, cancellationToken);
            if (latest == null)
            {
                throw new OAuthProtocolException(
                    ex.Error,
                    $"Model key {modelKey.Id} not found after refresh_token_reused.",
                    ex.StatusCode);
            }

            OpenAIModelOAuthConfig reloaded = OpenAIModelOAuthConfig.Parse(latest.OAuthConfigJson);
            DateTime? reloadedExpiresAt = reloaded.ParseAccessTokenExpiresAtUtc();
            if (!string.IsNullOrWhiteSpace(reloaded.AccessToken)
                && reloadedExpiresAt != null
                && reloadedExpiresAt > DateTime.UtcNow.AddSeconds(15))
            {
                return reloaded.AccessToken;
            }

            if (!string.IsNullOrWhiteSpace(reloaded.AccessToken))
            {
                return reloaded.AccessToken;
            }

            throw new OAuthProtocolException(
                ex.Error,
                "Refresh token has been rotated but no newer access token was found locally. Please sign in again.",
                ex.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenAI oauth refresh failed for model key {ModelKeyId}", modelKey.Id);
            await SaveLastErrorAsync(modelKey.Id, ex.Message, cancellationToken);
            throw;
        }
        finally
        {
            modelLock.Release();
        }
    }

    public async Task<string> GetBearerTokenForModelKeyAsync(ModelKey modelKey, string endpoint, CancellationToken cancellationToken)
    {
        // Only keep the first auth mode: use OAuth access token as Bearer directly.
        _ = endpoint;
        return await GetAccessTokenForModelKeyAsync(modelKey, cancellationToken);
    }

    public async Task<OpenAIModelOAuthStatusResult> GetStatusAsync(short modelKeyId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        ModelKey? modelKey = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            throw new OAuthProtocolException("invalid_request", $"Model key {modelKeyId} not found.");
        }

        OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(modelKey.OAuthConfigJson);
        DateTime? expiresAt = config.ParseAccessTokenExpiresAtUtc();
        return new OpenAIModelOAuthStatusResult
        {
            ModelKeyId = modelKeyId,
            Connected = !string.IsNullOrWhiteSpace(config.AccessToken) || !string.IsNullOrWhiteSpace(config.RefreshToken),
            HasRefreshToken = !string.IsNullOrWhiteSpace(config.RefreshToken),
            AccessTokenExpiresAtUtc = expiresAt,
            LastError = config.LastError,
            UpdatedAtUtc = DateTime.TryParse(config.UpdatedAtUtc, out DateTime updatedAt) ? DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc) : null,
        };
    }

    public async Task DisconnectAsync(short modelKeyId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        ModelKey? modelKey = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            throw new OAuthProtocolException("invalid_request", $"Model key {modelKeyId} not found.");
        }

        OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(modelKey.OAuthConfigJson);
        OpenAIModelOAuthConfig updated = config with
        {
            AccessToken = null,
            RefreshToken = null,
            IdToken = null,
            ApiKeyAccessToken = null,
            AccessTokenExpiresAtUtc = null,
            LastError = null,
            UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
        };
        modelKey.OAuthConfigJson = updated.ToJson();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveLastErrorAsync(short modelKeyId, string message, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        ModelKey? modelKey = await db.ModelKeys.FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return;
        }
        OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(modelKey.OAuthConfigJson);
        modelKey.OAuthConfigJson = (config with
        {
            LastError = message.Length > 500 ? message[..500] : message,
            UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
        }).ToJson();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool ContainsRefreshTokenReusedError(string message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && (message.Contains("refresh_token_reused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("refresh token has already been used", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveAllowedWorkspaceId(OpenAIModelOAuthConfig config)
    {
        return string.IsNullOrWhiteSpace(config.AllowedWorkspaceId)
            ? null
            : config.AllowedWorkspaceId;
    }

    private static string? ExtractFirstOrganizationId(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            string[] parts = idToken.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            int padding = payload.Length % 4;
            if (padding > 0)
            {
                payload = payload.PadRight(payload.Length + (4 - padding), '=');
            }

            byte[] bytes = Convert.FromBase64String(payload);
            using JsonDocument doc = JsonDocument.Parse(bytes);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("https://api.openai.com/auth", out JsonElement auth)
                || auth.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!auth.TryGetProperty("organizations", out JsonElement organizations)
                || organizations.ValueKind != JsonValueKind.Array
                || organizations.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = organizations[0];
            if (first.ValueKind != JsonValueKind.Object
                || !first.TryGetProperty("id", out JsonElement idElement))
            {
                return null;
            }

            string? orgId = idElement.GetString();
            return string.IsNullOrWhiteSpace(orgId) ? null : orgId;
        }
        catch
        {
            return null;
        }
    }

    private async Task<OAuthTokenEndpointResponse> RefreshAccessTokenAsync(
        string tokenEndpoint,
        string clientId,
        string? clientSecret,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
            ["scope"] = RefreshScope,
        };
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            form["client_secret"] = clientSecret;
        }

        return await RequestTokenAsync(tokenEndpoint, form, cancellationToken);
    }

    private async Task<OAuthTokenEndpointResponse> RequestTokenAsync(string tokenEndpoint, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new OAuthProtocolException("invalid_grant", $"Token endpoint failed: {body}", (int)response.StatusCode);
        }

        OAuthTokenEndpointResponse? token = System.Text.Json.JsonSerializer.Deserialize<OAuthTokenEndpointResponse>(body);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new OAuthProtocolException("invalid_grant", "Token endpoint response missing access_token.");
        }

        return token;
    }

    private static string ResolveAuthorizationEndpoint(OpenAIModelOAuthConfig config, string issuer)
    {
        if (!string.IsNullOrWhiteSpace(config.AuthorizationEndpoint))
        {
            return config.AuthorizationEndpoint;
        }
        return $"{issuer.TrimEnd('/')}/oauth/authorize";
    }

    private static string ResolveTokenEndpoint(OpenAIModelOAuthConfig config, string issuer)
    {
        return HardcodedOpenAITokenEndpoint;
    }

    private static string ResolveIssuer(OpenAIModelOAuthConfig config)
    {
        return string.IsNullOrWhiteSpace(config.Issuer) ? DefaultIssuer : config.Issuer!;
    }

    private static string ResolveClientId(OpenAIModelOAuthConfig config)
    {
        return string.IsNullOrWhiteSpace(config.ClientId) ? DefaultClientId : config.ClientId!;
    }

    private record OAuthTokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }
    }
}

public record OpenAIModelOAuthStartResult
{
    public required string AuthorizationUrl { get; init; }
    public required string State { get; init; }
    public required string RedirectUri { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}

public record OpenAIModelOAuthCallbackResult
{
    public required bool Success { get; init; }
    public required short ModelKeyId { get; init; }
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }
}

public record OpenAIModelOAuthStatusResult
{
    public required short ModelKeyId { get; init; }
    public required bool Connected { get; init; }
    public required bool HasRefreshToken { get; init; }
    public required DateTime? AccessTokenExpiresAtUtc { get; init; }
    public required string? LastError { get; init; }
    public required DateTime? UpdatedAtUtc { get; init; }
}

