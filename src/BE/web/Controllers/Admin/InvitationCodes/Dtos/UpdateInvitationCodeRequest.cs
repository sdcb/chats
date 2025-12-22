using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.InvitationCodes.Dtos;

public record UpdateInvitationCodeRequest
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("count")]
    public required short Count { get; init; }
}

public record CreateInvitationCodeRequest
{
    [JsonPropertyName("value")]
    public required string Name { get; init; }

    [JsonPropertyName("count")]
    public required short Count { get; init; }
}