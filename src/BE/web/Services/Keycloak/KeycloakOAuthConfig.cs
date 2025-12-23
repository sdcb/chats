using System.Text.Json.Serialization;

namespace Chats.BE.Services.Keycloak;

public record KeycloakOAuthConfig
{
    // 必需的核心字段
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    // 可选但常用的字段
    [JsonPropertyName("userinfo_endpoint")]
    public string? UserinfoEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; init; }

    [JsonPropertyName("end_session_endpoint")]
    public string? EndSessionEndpoint { get; init; }
}
