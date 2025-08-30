using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.UrlEncryption;

namespace Chats.BE.Controllers.Chats.ChatPresets.Dtos;

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
                ModelProviderId = x.ChatConfig.Model.ModelReference.ProviderId,
                Temperature = x.ChatConfig.Temperature,
                WebSearchEnabled = x.ChatConfig.WebSearchEnabled,
                MaxOutputTokens = x.ChatConfig.MaxOutputTokens,
                ReasoningEffort = x.ChatConfig.ReasoningEffort,
                ImageSize =  (DBKnownImageSize)x.ChatConfig.ImageSizeId,
                Mcps = [.. x.ChatConfig.ChatConfigMcps.Select(mcp => new ChatSpanMcp
                {
                    Id = mcp.McpServerId,
                    CustomHeaders = mcp.Headers
                })]
            })]
        };
    }
}
