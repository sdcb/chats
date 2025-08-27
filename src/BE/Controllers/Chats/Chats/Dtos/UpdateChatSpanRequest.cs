using Chats.BE.DB;
using Chats.BE.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record UpdateChatSpanRequest
{
    [JsonPropertyName("modelId")]
    public short ModelId { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; init; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    [JsonPropertyName("webSearchEnabled")]
    public bool WebSearchEnabled { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("reasoningEffort")]
    public DBReasoningEffort ReasoningEffort { get; init; }

    [JsonPropertyName("imageSizeId")]
    public DBKnownImageSize ImageSize { get; init; }

    [JsonPropertyName("mcpIds")]
    public List<int> McpIds { get; init; } = [];

    public void ApplyTo(ChatSpan span)
    {
        span.Enabled = Enabled;

        ChatConfig config = span.ChatConfig ?? throw new InvalidOperationException("ChatSpan.ChatConfig is null");
        config.ModelId = ModelId;
        config.SystemPrompt = string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt;
        config.Temperature = Temperature;
        config.WebSearchEnabled = WebSearchEnabled;
        config.MaxOutputTokens = MaxOutputTokens;
        config.ReasoningEffort = (byte)ReasoningEffort;
        config.ImageSizeId = (short)ImageSize;
        
        // Update ChatConfigMcp associations
        UpdateMcpAssociations(config);
    }

    public void ApplyTo(ChatPresetSpan span, Model model)
    {
        if (model.Id != ModelId)
        {
            throw new ArgumentException("ModelId does not match the provided model", nameof(ModelId));
        }

        span.Enabled = Enabled;

        ChatConfig config = span.ChatConfig ?? throw new InvalidOperationException("ChatPresetSpan.ChatConfig is null");
        config.ModelId = ModelId;
        config.SystemPrompt = string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt;
        config.Temperature = Temperature;
        config.WebSearchEnabled = WebSearchEnabled;
        config.MaxOutputTokens = MaxOutputTokens;
        config.ReasoningEffort = (byte)ReasoningEffort;
        config.ImageSizeId = (short)ImageSize;
        
        // Update ChatConfigMcp associations
        UpdateMcpAssociations(config);
    }

    public ChatPresetSpan ToDB(Model model, byte spanId)
    {
        if (model.Id != ModelId)
        {
            throw new ArgumentException("ModelId does not match the provided model", nameof(ModelId));
        }

        ChatConfig chatConfig = new ChatConfig()
        {
            ModelId = ModelId,
            Model = model,
            SystemPrompt = string.IsNullOrEmpty(SystemPrompt) ? null : SystemPrompt,
            Temperature = Temperature,
            WebSearchEnabled = WebSearchEnabled,
            MaxOutputTokens = MaxOutputTokens,
            ReasoningEffort = (byte)ReasoningEffort,
            ImageSizeId = (short)ImageSize,
        };

        ChatPresetSpan presetSpan = new ChatPresetSpan()
        {
            SpanId = spanId, 
            Enabled = Enabled,
            ChatConfig = chatConfig,
        };

        // Add ChatConfigMcp associations
        foreach (int mcpId in McpIds)
        {
            chatConfig.ChatConfigMcps.Add(new ChatConfigMcp
            {
                ChatConfig = chatConfig,
                McpServerId = mcpId
            });
        }

        return presetSpan;
    }

    private void UpdateMcpAssociations(ChatConfig config)
    {
        // Get existing MCP IDs
        HashSet<int> existingMcpIds = config.ChatConfigMcps.Select(x => x.McpServerId).ToHashSet();
        HashSet<int> newMcpIds = McpIds.ToHashSet();

        // Remove associations that are no longer needed
        List<ChatConfigMcp> toRemove = config.ChatConfigMcps.Where(x => !newMcpIds.Contains(x.McpServerId)).ToList();
        foreach (ChatConfigMcp? item in toRemove)
        {
            config.ChatConfigMcps.Remove(item);
        }

        // Add new associations
        foreach (int mcpId in newMcpIds.Where(id => !existingMcpIds.Contains(id)))
        {
            config.ChatConfigMcps.Add(new ChatConfigMcp
            {
                ChatConfig = config,
                McpServerId = mcpId
            });
        }
    }
}
