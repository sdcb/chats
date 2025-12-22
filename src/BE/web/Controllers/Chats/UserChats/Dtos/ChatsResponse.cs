using Chats.Web.Controllers.Chats.Messages.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.UserChats.Dtos;

public record ChatsResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("isTopMost")]
    public required bool IsTopMost { get; init; }

    [JsonPropertyName("isShared")]
    public required bool IsShared { get; init; }

    [JsonPropertyName("spans")]
    public required ChatSpanDto[] Spans { get; init; }

    [JsonPropertyName("groupId")]
    public required string? GroupId { get; init; }

    [JsonPropertyName("tags")]
    public required string[] Tags { get; init; }

    [JsonPropertyName("leafMessageId")]
    public required string? LeafTurnId { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; init; }

    public ChatsResponseWithMessage WithMessages(TurnDto[] messages)
    {
        return new ChatsResponseWithMessage
        {
            Id = Id,
            Title = Title,
            IsTopMost = IsTopMost,
            IsShared = IsShared,
            Spans = Spans,
            GroupId = GroupId,
            Tags = Tags,
            LeafTurnId = LeafTurnId,
            UpdatedAt = UpdatedAt,
            Messages = messages,
        };
    }
}

public record ChatsResponseWithMessage : ChatsResponse
{
    [JsonPropertyName("messages")]
    public required TurnDto[] Messages { get; set; }
}

public record ChatSpanDto
{
    [JsonPropertyName("spanId")]
    public required byte SpanId { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("systemPrompt")]
    public required string? SystemPrompt { get; init; }

    [JsonPropertyName("modelId")]
    public required int ModelId { get; init; }

    [JsonPropertyName("modelName")]
    public required string ModelName { get; init; }

    [JsonPropertyName("modelProviderId")]
    public required short ModelProviderId { get; init; }

    [JsonPropertyName("temperature")]
    public required float? Temperature { get; init; }

    [JsonPropertyName("webSearchEnabled")]
    public required bool WebSearchEnabled { get; init; }

    [JsonPropertyName("codeExecutionEnabled")]
    public required bool CodeExecutionEnabled { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public required int? MaxOutputTokens { get; init; }

    [JsonPropertyName("reasoningEffort")]
    public required DBReasoningEffort? ReasoningEffort { get; init; }

    [JsonPropertyName("imageSize")]
    public required string? ImageSize { get; init; }

    [JsonPropertyName("thinkingBudget")]
    public required int? ThinkingBudget { get; init; }

    [JsonPropertyName("mcps")]
    public required ChatSpanMcp[] Mcps { get; init; }

    public static ChatSpanDto FromDB(ChatSpan span) => new()
    {
        SpanId = span.SpanId,
        Enabled = span.Enabled,
        SystemPrompt = span.ChatConfig.SystemPrompt,
        ModelId = span.ChatConfig.ModelId,
        ModelName = span.ChatConfig.Model.Name,
        ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
        Temperature = span.ChatConfig.Temperature,
        WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
        CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
        MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
        ReasoningEffort = span.ChatConfig.ReasoningEffort,
        ImageSize = span.ChatConfig.ImageSize,
        ThinkingBudget = span.ChatConfig.ThinkingBudget,
        Mcps = [.. span.ChatConfig.ChatConfigMcps.Select(
            x => new ChatSpanMcp
            {
                Id = x.McpServerId,
                CustomHeaders = x.CustomHeaders
            })],
    };

    public static ChatSpanDto FromDB(ChatPresetSpan span) => new()
    {
        SpanId = span.SpanId,
        Enabled = span.Enabled,
        SystemPrompt = span.ChatConfig.SystemPrompt,
        ModelId = span.ChatConfig.ModelId,
        ModelName = span.ChatConfig.Model.Name,
        ModelProviderId = span.ChatConfig.Model.ModelKey.ModelProviderId,
        Temperature = span.ChatConfig.Temperature,
        WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
        CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
        MaxOutputTokens = span.ChatConfig.MaxOutputTokens,
        ReasoningEffort = span.ChatConfig.ReasoningEffort,
        ImageSize = span.ChatConfig.ImageSize,
        ThinkingBudget = span.ChatConfig.ThinkingBudget,
        Mcps = [.. span.ChatConfig.ChatConfigMcps.Select(
            x => new ChatSpanMcp
            {
                Id = x.McpServerId,
                CustomHeaders = x.CustomHeaders
            })],
    };
}

public record ChatSpanMcp
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("customHeaders")]
    public string? CustomHeaders { get; init; }

    public string? GetNormalizedCustomHeaders() => string.IsNullOrWhiteSpace(CustomHeaders) ? null : CustomHeaders.Trim();
}