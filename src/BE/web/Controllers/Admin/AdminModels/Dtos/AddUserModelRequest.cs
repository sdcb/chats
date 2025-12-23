using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

public record AddUserModelRequest
{
    [JsonPropertyName("userId")]
    public required int UserId { get; init; }

    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    [JsonPropertyName("tokens"), Range(0, int.MaxValue / 2)]
    public required int Tokens { get; init; }

    [JsonPropertyName("counts"), Range(0, int.MaxValue / 2)]
    public required int Counts { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }
}
