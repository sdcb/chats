using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Public.AccountLogin.Dtos;

public record LoginResponse
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("username")]
    public required string? UserName { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("canReCharge")]
    public required bool CanReCharge { get; init; }
}