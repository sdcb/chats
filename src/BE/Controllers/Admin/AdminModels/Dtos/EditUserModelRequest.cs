using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminModels.Dtos;

public record EditUserModelRequest
{
    [JsonPropertyName("tokens"), Range(0, int.MaxValue / 2)]
    public required int Tokens { get; init; }

    [JsonPropertyName("counts"), Range(0, int.MaxValue / 2)]
    public required int Counts { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }
}
