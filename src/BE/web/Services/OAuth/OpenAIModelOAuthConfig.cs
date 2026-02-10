using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.OAuth;

public record OpenAIModelOAuthConfig
{
    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("authorizationEndpoint")]
    public string? AuthorizationEndpoint { get; init; }

    [JsonPropertyName("tokenEndpoint")]
    public string? TokenEndpoint { get; init; }

    [JsonPropertyName("redirectUri")]
    public string? RedirectUri { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("originator")]
    public string? Originator { get; init; }

    [JsonPropertyName("idTokenAddOrganizations")]
    public bool? IdTokenAddOrganizations { get; init; }

    [JsonPropertyName("codexCliSimplifiedFlow")]
    public bool? CodexCliSimplifiedFlow { get; init; }

    [JsonPropertyName("allowedWorkspaceId")]
    public string? AllowedWorkspaceId { get; init; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("idToken")]
    public string? IdToken { get; init; }

    [JsonPropertyName("apiKeyAccessToken")]
    public string? ApiKeyAccessToken { get; init; }

    [JsonPropertyName("accessTokenExpiresAtUtc")]
    public string? AccessTokenExpiresAtUtc { get; init; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public string? UpdatedAtUtc { get; init; }

    public DateTime? ParseAccessTokenExpiresAtUtc()
    {
        if (DateTime.TryParse(AccessTokenExpiresAtUtc, out DateTime dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        return null;
    }

    public static OpenAIModelOAuthConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OpenAIModelOAuthConfig();
        }

        try
        {
            OpenAIModelOAuthConfig? parsed = JsonSerializer.Deserialize<OpenAIModelOAuthConfig>(json);
            return parsed ?? new OpenAIModelOAuthConfig();
        }
        catch (JsonException)
        {
            return new OpenAIModelOAuthConfig();
        }
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }
}
