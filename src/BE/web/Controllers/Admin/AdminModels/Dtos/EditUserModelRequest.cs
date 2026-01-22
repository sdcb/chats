using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

public record EditUserModelRequest
{
    [JsonPropertyName("tokensDelta")]
    public required int TokensDelta { get; init; }

    [JsonPropertyName("countsDelta")]
    public required int CountsDelta { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }
}
