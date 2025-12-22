using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.InvitationCodes.Dtos;

public record InvitationCodeDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("count")]
    public required int Count { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }
}
