using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.AnthropicCompatible.Dtos;

/// <summary>
/// Anthropic Messages API v1 response format (non-streaming)
/// </summary>
public record AnthropicResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; } = "message";

    [JsonPropertyName("role")]
    public string Role { get; } = "assistant";

    [JsonPropertyName("content")]
    public required List<AnthropicResponseContentBlock> Content { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("stop_reason")]
    public required string? StopReason { get; init; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }

    [JsonPropertyName("usage")]
    public required AnthropicUsage Usage { get; init; }
}

public record AnthropicResponseContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    // For text blocks
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

    public static AnthropicResponseContentBlock FromText(string text)
    {
        return new AnthropicResponseContentBlock
        {
            Type = "text",
            Text = text
        };
    }

    public static AnthropicResponseContentBlock FromThinking(string thinking, string? signature)
    {
        return new AnthropicResponseContentBlock
        {
            Type = "thinking",
            Thinking = thinking,
            Signature = signature
        };
    }

    public static AnthropicResponseContentBlock FromToolUse(string id, string name, object input)
    {
        return new AnthropicResponseContentBlock
        {
            Type = "tool_use",
            Id = id,
            Name = name,
            Input = input
        };
    }
}

public record AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public required int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public required int OutputTokens { get; init; }

    [JsonPropertyName("cache_creation_input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("cache_read_input_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheReadInputTokens { get; init; }
}
