using System.Text.Json.Serialization;

namespace Chats.BE.Services.Keycloak;

public record SsoTokenDto
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    // 可选字段，提高兼容性
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }
}