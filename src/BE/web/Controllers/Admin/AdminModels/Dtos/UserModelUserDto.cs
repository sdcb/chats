using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

public record UserModelUserDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("userId")]
    public required int UserId { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    [JsonPropertyName("counts")]
    public required int Counts { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }
}
