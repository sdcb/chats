using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.AnthropicCompatible.Dtos;

/// <summary>
/// Anthropic Messages API v1 streaming event types
/// </summary>
public abstract record AnthropicStreamEvent
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// event: message_start
/// </summary>
public record MessageStartEvent : AnthropicStreamEvent
{
    public override string Type => "message_start";

    [JsonPropertyName("message")]
    public required MessageStartData Message { get; init; }
}

public record MessageStartData
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; } = "message";

    [JsonPropertyName("role")]
    public string Role { get; } = "assistant";

    [JsonPropertyName("content")]
    public List<object> Content { get; } = [];

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }

    [JsonPropertyName("usage")]
    public required MessageStartUsage Usage { get; init; }
}

public record MessageStartUsage
{
    [JsonPropertyName("input_tokens")]
    public required int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; } = 0;

    [JsonPropertyName("cache_creation_input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("cache_read_input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheReadInputTokens { get; init; }
}

/// <summary>
/// event: content_block_start
/// </summary>
public record ContentBlockStartEvent : AnthropicStreamEvent
{
    public override string Type => "content_block_start";

    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("content_block")]
    public required ContentBlockStartData ContentBlock { get; init; }
}

public record ContentBlockStartData
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    // For text blocks - empty string
    [JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    // For tool_use blocks
    [JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("input"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Input { get; init; }

    // For thinking blocks
    [JsonPropertyName("thinking"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Thinking { get; init; }

    [JsonPropertyName("signature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Signature { get; init; }

    public static ContentBlockStartData CreateText() => new() { Type = "text", Text = "" };
    public static ContentBlockStartData CreateThinking() => new() { Type = "thinking", Thinking = "", Signature = "" };
    public static ContentBlockStartData CreateToolUse(string id, string name) => new() { Type = "tool_use", Id = id, Name = name, Input = new { } };
}

/// <summary>
/// event: content_block_delta
/// </summary>
public record ContentBlockDeltaEvent : AnthropicStreamEvent
{
    public override string Type => "content_block_delta";

    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("delta")]
    public required ContentBlockDelta Delta { get; init; }
}

public record ContentBlockDelta
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    // For text_delta
    [JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    // For input_json_delta
    [JsonPropertyName("partial_json"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PartialJson { get; init; }

    // For thinking_delta
    [JsonPropertyName("thinking"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Thinking { get; init; }

    // For signature_delta
    [JsonPropertyName("signature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Signature { get; init; }

    public static ContentBlockDelta TextDelta(string text) => new() { Type = "text_delta", Text = text };
    public static ContentBlockDelta ThinkingDelta(string thinking) => new() { Type = "thinking_delta", Thinking = thinking };
    public static ContentBlockDelta SignatureDelta(string signature) => new() { Type = "signature_delta", Signature = signature };
    public static ContentBlockDelta InputJsonDelta(string partialJson) => new() { Type = "input_json_delta", PartialJson = partialJson };
}

/// <summary>
/// event: content_block_stop
/// </summary>
public record ContentBlockStopEvent : AnthropicStreamEvent
{
    public override string Type => "content_block_stop";

    [JsonPropertyName("index")]
    public required int Index { get; init; }
}

/// <summary>
/// event: message_delta
/// </summary>
public record MessageDeltaEvent : AnthropicStreamEvent
{
    public override string Type => "message_delta";

    [JsonPropertyName("delta")]
    public required MessageDelta Delta { get; init; }

    [JsonPropertyName("usage")]
    public required MessageDeltaUsage Usage { get; init; }
}

public record MessageDelta
{
    [JsonPropertyName("stop_reason")]
    public required string? StopReason { get; init; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }
}

public record MessageDeltaUsage
{
    [JsonPropertyName("input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public required int OutputTokens { get; init; }

    [JsonPropertyName("cache_creation_input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("cache_read_input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheReadInputTokens { get; init; }
}

/// <summary>
/// event: message_stop
/// </summary>
public record MessageStopEvent : AnthropicStreamEvent
{
    public override string Type => "message_stop";
}

/// <summary>
/// event: ping
/// </summary>
public record PingEvent : AnthropicStreamEvent
{
    public override string Type => "ping";
}

/// <summary>
/// event: error
/// </summary>
public record ErrorEvent : AnthropicStreamEvent
{
    public override string Type => "error";

    [JsonPropertyName("error")]
    public required AnthropicErrorDetail Error { get; init; }
}
