using Chats.BE.Services.Models.ChatServices;
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
}

public record OpenAIFullResponse
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string? Content { get; init; }

    [JsonPropertyName("reasoning_content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? ReasoningContent { get; init; }

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

    [JsonPropertyName("completion_tokens_details")]
    public CompletionTokensDetails? CompletionTokensDetails { get; init; }
}

public record CompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public required int ReasoningTokens { get; init; }
}