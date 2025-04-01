using Chats.BE.Services.Models.ChatServices;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.OpenAICompatible.Dtos;

public record OpenAIDelta
{
    [JsonPropertyName("content")]
    public required string? Content { get; init; }

    [JsonPropertyName("reasoning_content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? ReasoningContent { get; init; }

    [JsonPropertyName("image"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required ChatRespImage? Image { get; init; }
}

public record DeltaChoice
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("delta")]
    public required OpenAIDelta Delta { get; init; }

    [JsonPropertyName("logprobs")]
    public object? Logprobs { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public record ChatCompletionChunk
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public required string Object { get; init; }

    [JsonPropertyName("created")]
    public required long Created { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; init; }

    [JsonPropertyName("choices")]
    public required List<DeltaChoice> Choices { get; init; }

    [JsonPropertyName("usage")]
    public Usage? Usage { get; init; }
}