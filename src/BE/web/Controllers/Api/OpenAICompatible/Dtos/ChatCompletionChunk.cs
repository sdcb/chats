using Chats.BE.Services.Models.ChatServices;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Api.OpenAICompatible.Dtos;

public record OpenAIDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("reasoning_content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningContent { get; init; }

    [JsonPropertyName("image"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageChatSegment? Image { get; init; }

    [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIToolCallSegment[]? ToolCalls { get; init; }
}

/// <summary>tool_calls[*].function</summary>
public record OpenAIToolCallSegmentFunction
{
    // name 只会在第一次出现时给出，后续增量片段里可能缺失
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    // 每个增量片段都会继续把 arguments 以字符串形式拼接过来
    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

/// <summary>tool_calls[*]</summary>
public record OpenAIToolCallSegment
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    // 只在首个增量片段带 id / type
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonPropertyName("function")]
    public required OpenAIToolCallSegmentFunction Function { get; init; }
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
    public string Object { get; } = "chat.completion.chunk";

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