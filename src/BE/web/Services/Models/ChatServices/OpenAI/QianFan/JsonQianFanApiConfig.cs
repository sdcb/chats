using System.Text.Json.Serialization;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;

public record JsonQianFanApiConfig
{
    [JsonPropertyName("appId")]
    public required string AppId { get; init; }

    [JsonPropertyName("apiKey")]
    public required string ApiKey { get; init; }
}