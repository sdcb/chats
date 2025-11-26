using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.AnthropicCompatible.Dtos;

/// <summary>
/// Anthropic Messages API v1 request format
/// </summary>
public record AnthropicRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    [JsonPropertyName("messages")]
    public required List<AnthropicMessage> Messages { get; init; }

    [JsonPropertyName("system")]
    public string? System { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; init; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; init; }

    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; init; }

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; init; }

    [JsonPropertyName("metadata")]
    public AnthropicMetadata? Metadata { get; init; }

    [JsonPropertyName("thinking")]
    public AnthropicThinkingConfig? Thinking { get; init; }
}

public record AnthropicMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required object Content { get; init; } // Can be string or array of content blocks
}

public record AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    // For text blocks
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    // For image blocks
    [JsonPropertyName("source")]
    public AnthropicImageSource? Source { get; init; }

    // For tool_use blocks
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("input")]
    public object? Input { get; init; }

    // For tool_result blocks
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    [JsonPropertyName("content")]
    public object? ResultContent { get; init; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }

    // For thinking blocks
    [JsonPropertyName("thinking")]
    public string? Thinking { get; init; }

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }
}

public record AnthropicImageSource
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "base64" or "url"

    [JsonPropertyName("media_type")]
    public string? MediaType { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public record AnthropicTool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("input_schema")]
    public required object InputSchema { get; init; }
}

public record AnthropicMetadata
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }
}

public record AnthropicThinkingConfig
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "enabled" or "disabled"

    [JsonPropertyName("budget_tokens")]
    public int? BudgetTokens { get; init; }
}
