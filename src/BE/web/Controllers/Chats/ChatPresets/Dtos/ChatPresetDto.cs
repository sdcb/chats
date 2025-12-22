using Chats.Web.Controllers.Chats.UserChats.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Services.UrlEncryption;

namespace Chats.Web.Controllers.Chats.ChatPresets.Dtos;

public record ChatPresetDto
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required DateTime UpdatedAt { get; init; }

    public required ChatSpanDto[] Spans { get; init; }

    public static ChatPresetDto FromDB(ChatPreset preset, IUrlEncryptionService idEncryption)
    {
        if (preset.ChatPresetSpans == null)
        {
            throw new InvalidOperationException("ChatPresetSpans must be loaded");
        }

        return new ChatPresetDto
        {
            Id = idEncryption.EncryptChatPresetId(preset.Id),
            Name = preset.Name,
            UpdatedAt = preset.UpdatedAt,
            Spans = [.. preset.ChatPresetSpans.Select(x => new ChatSpanDto
            {
                SpanId = x.SpanId,
                Enabled = x.Enabled,
                SystemPrompt = x.ChatConfig.SystemPrompt,
                ModelId = x.ChatConfig.ModelId,
                ModelName = x.ChatConfig.Model.Name,
                ModelProviderId = x.ChatConfig.Model.ModelKey.ModelProviderId,
                Temperature = x.ChatConfig.Temperature,
                WebSearchEnabled = x.ChatConfig.WebSearchEnabled,
                CodeExecutionEnabled = x.ChatConfig.CodeExecutionEnabled,
                MaxOutputTokens = x.ChatConfig.MaxOutputTokens,
                ReasoningEffort = x.ChatConfig.ReasoningEffort,
                ImageSize = x.ChatConfig.ImageSize,
                ThinkingBudget = x.ChatConfig.ThinkingBudget,
                Mcps = [.. x.ChatConfig.ChatConfigMcps.Select(mcp => new ChatSpanMcp
                {
                    Id = mcp.McpServerId,
                    CustomHeaders = mcp.CustomHeaders
                })]
            })]
        };
    }
}
