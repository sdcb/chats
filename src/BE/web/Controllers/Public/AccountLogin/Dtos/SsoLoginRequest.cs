using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Public.AccountLogin.Dtos;

public record SsoLoginRequest
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("provider"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? Provider { get; init; }
}
