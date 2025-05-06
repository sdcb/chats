using Chats.BE.Services.Models.Dtos;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.OpenAICompatible.Dtos;

public record FullChatCompletion
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public required string Object { get; init; }

    [JsonPropertyName("created")]
    public required long Created { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("choices")]
    public required List<MessageChoice> Choices { get; init; }

    [JsonPropertyName("usage")]
    public required Usage Usage { get; init; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; init; }

    public ChatCompletionChunk ToFinalChunk()
    {
        return new ChatCompletionChunk
        {
            Id = Id,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = Model,
            Choices = [],
            Usage = Usage,
            SystemFingerprint = SystemFingerprint,
        };
    }
}

public record MessageChoice
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("message")]
    public required OpenAIFullResponse Message { get; init; }

    [JsonPropertyName("logprobs")]
    public object? Logprobs { get; init; }

    [JsonPropertyName("finish_reason")]
    public required string? FinishReason { get; init; }

    public ChatCompletionChunk ToFinalChunk(string modelName, string traceId, string? systemFingerprint)
    {
        return new ChatCompletionChunk
        {
            Id = traceId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            Choices =
            [
                new DeltaChoice()
                {
                    Index = Index,
                    Delta = new OpenAIDelta(){},
                    Logprobs = Logprobs,
                    FinishReason = FinishReason,
                }
            ],
            Usage = null,
            SystemFingerprint = systemFingerprint,
        };
    }
}

public record OpenAIFullResponse
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string? Content { get; init; }

    [JsonPropertyName("reasoning_content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningContent { get; init; }

    [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FullToolCall[]? ToolCalls { get; init; }

    [JsonPropertyName("segments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required ICollection<ChatSegmentItem> Segments { get; init; }

    [JsonPropertyName("refusal")]
    public object? Refusal { get; init; }
}

public record Usage
{
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public required int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }

    [JsonPropertyName("prompt_tokens_details")]
    public PromptTokensDetails? PromptTokensDetails { get; init; }

    [JsonPropertyName("completion_tokens_details")]
    public CompletionTokensDetails? CompletionTokensDetails { get; init; }
}

public record PromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public required int CachedTokens { get; init; }

    [JsonPropertyName("audio_tokens")]
    public required int AudioTokens { get; init; }
}

public record CompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public required int ReasoningTokens { get; init; }

    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; init; }

    [JsonPropertyName("accepted_prediction_tokens")]
    public int AcceptedPredictionTokens { get; init; }

    [JsonPropertyName("rejected_prediction_tokens")]
    public int RejectedPredictionTokens { get; init; }
}

public record FullToolCallFunction
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

public record FullToolCall
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }   // OpenAI 目前恒为 "function"

    [JsonPropertyName("function")]
    public required FullToolCallFunction Function { get; init; }
}