using Chats.DB;
using Chats.DB.Enums;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Chats.BE.Services.OAuth;

public class OpenAIOAuthRequestHelper(OpenAIModelOAuthService? openAIModelOAuthService = null)
{
    public const string ChatGptBackendCodexBase = "https://chatgpt.com/backend-api/codex";
    public const string DefaultCodexModel = "gpt-5.2-codex";
    public const string DefaultModelsClientVersion = "0.98.0";

    public bool IsOpenAIOAuthWithOpenAIProvider(ModelKey modelKey)
    {
        return modelKey.AuthType == DBModelAuthType.OAuth
            && modelKey.ModelProviderId == (short)DBModelProvider.OpenAI;
    }

    public bool IsChatGptBackendCodex(string endpoint)
    {
        return endpoint.Contains("chatgpt.com/backend-api/codex", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains("chat.openai.com/backend-api/codex", StringComparison.OrdinalIgnoreCase);
    }

    public bool UseCodexOAuthCompat(ModelKey modelKey, string endpoint)
    {
        return IsOpenAIOAuthWithOpenAIProvider(modelKey) && IsChatGptBackendCodex(endpoint);
    }

    public string ResolveEndpoint(ModelKey modelKey, string fallbackEndpoint)
    {
        if (IsOpenAIOAuthWithOpenAIProvider(modelKey))
        {
            return ChatGptBackendCodexBase;
        }

        return fallbackEndpoint.TrimEnd('/');
    }

    public string ResolveModelsPath(bool useCodexOAuthCompat, bool v1Path)
    {
        if (useCodexOAuthCompat)
        {
            return v1Path
                ? $"/v1/models?client_version={DefaultModelsClientVersion}"
                : $"/models?client_version={DefaultModelsClientVersion}";
        }

        return v1Path ? "/v1/models" : "/models";
    }

    public string ResolveDeploymentName(string deploymentName, bool useCodexOAuthCompat)
    {
        if (useCodexOAuthCompat &&
            !deploymentName.Contains("codex", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultCodexModel;
        }

        return deploymentName;
    }

    public void ApplyAuthorizationHeaders(HttpRequestMessage request, ModelKey modelKey, string endpoint)
    {
        string bearerToken = ResolveBearerToken(modelKey, endpoint, CancellationToken.None);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.TryAddWithoutValidation("User-Agent", "codex-cli");
        if (UseCodexOAuthCompat(modelKey, endpoint))
        {
            string? accountId = TryResolveChatGptAccountId(modelKey);
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                request.Headers.Remove("ChatGPT-Account-Id");
                request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
            }
        }
    }

    private string ResolveBearerToken(ModelKey modelKey, string endpoint, CancellationToken cancellationToken)
    {
        if (modelKey.AuthType == DBModelAuthType.OAuth && openAIModelOAuthService != null)
        {
            return openAIModelOAuthService
                .GetBearerTokenForModelKeyAsync(modelKey, endpoint, cancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        return modelKey.Secret ?? throw new InvalidOperationException("Model key secret is null");
    }

    private static string? TryResolveChatGptAccountId(ModelKey modelKey)
    {
        OpenAIModelOAuthConfig config = OpenAIModelOAuthConfig.Parse(modelKey.OAuthConfigJson);
        string?[] candidates =
        [
            TryExtractJwtStringClaim(config.IdToken, "chatgpt_account_id"),
            TryExtractJwtNestedStringClaim(config.IdToken, "https://api.openai.com/auth", "chatgpt_account_id"),
            TryExtractJwtStringClaim(config.AccessToken, "chatgpt_account_id"),
            TryExtractJwtNestedStringClaim(config.AccessToken, "https://api.openai.com/auth", "chatgpt_account_id"),
            TryExtractJwtStringClaim(config.IdToken, "workspace_id"),
            TryExtractJwtNestedStringClaim(config.IdToken, "https://api.openai.com/auth", "workspace_id"),
            config.AllowedWorkspaceId,
        ];

        return candidates.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static string? TryExtractJwtStringClaim(string? jwt, string claimName)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = ParseJwtPayload(jwt);
            if (!doc.RootElement.TryGetProperty(claimName, out JsonElement claim)
                || claim.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? value = claim.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractJwtNestedStringClaim(string? jwt, string objClaim, string leafClaim)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = ParseJwtPayload(jwt);
            if (!doc.RootElement.TryGetProperty(objClaim, out JsonElement obj)
                || obj.ValueKind != JsonValueKind.Object
                || !obj.TryGetProperty(leafClaim, out JsonElement leaf)
                || leaf.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? value = leaf.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static JsonDocument ParseJwtPayload(string jwt)
    {
        string[] parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            throw new FormatException("Invalid JWT format.");
        }

        string payload = parts[1].Replace('-', '+').Replace('_', '/');
        int padding = payload.Length % 4;
        if (padding > 0)
        {
            payload = payload.PadRight(payload.Length + (4 - padding), '=');
        }

        byte[] bytes = Convert.FromBase64String(payload);
        return JsonDocument.Parse(bytes);
    }
}
