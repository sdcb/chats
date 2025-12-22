using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Messages.Dtos;

public record StepGenerateInfoDto
{
    [JsonPropertyName("inputCachedTokens")]
    public required int InputCachedTokens { get; init; }

    [JsonPropertyName("inputOverallTokens")]
    public required int InputOverallTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public required int OutputTokens { get; init; }

    [JsonPropertyName("inputFreshPrice")]
    public required decimal InputFreshPrice { get; init; }

    [JsonPropertyName("inputCachedPrice")]
    public required decimal InputCachedPrice { get; init; }

    [JsonPropertyName("inputPrice")]
    public required decimal InputPrice { get; init; }

    [JsonPropertyName("outputPrice")]
    public required decimal OutputPrice { get; init; }

    [JsonPropertyName("reasoningTokens")]
    public required int ReasoningTokens { get; init; }

    [JsonPropertyName("duration")]
    public required int Duration { get; init; }

    [JsonPropertyName("reasoningDuration")]
    public required int ReasoningDuration { get; init; }

    [JsonPropertyName("firstTokenLatency")]
    public required int FirstTokenLatency { get; init; }
}
