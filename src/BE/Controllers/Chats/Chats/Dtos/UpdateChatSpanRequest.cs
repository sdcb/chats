using Chats.BE.Controllers.Chats.UserChats.Dtos;
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

    [JsonPropertyName("mcps")]
    public required Dictionary<int, ChatSpanMcp> Mcps { get; init; }

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
        foreach (KeyValuePair<int, ChatSpanMcp> mcp in Mcps)
        {
            chatConfig.ChatConfigMcps.Add(new ChatConfigMcp
            {
                ChatConfig = chatConfig,
                McpServerId = mcp.Key,
                Headers = mcp.Value.CustomHeaders,
            });
        }

        return presetSpan;
    }

    private void UpdateMcpAssociations(ChatConfig config)
    {
        HashSet<int> currentMcpIds = [.. config.ChatConfigMcps.Select(x => x.McpServerId)];
        HashSet<int> requestMcpIds = [.. Mcps.Keys];
        HashSet<int> toRemove = [.. currentMcpIds.Except(requestMcpIds)];
        HashSet<int> toAdd = [.. requestMcpIds.Except(currentMcpIds)];
        HashSet<int> toUpdate = [.. currentMcpIds.Intersect(requestMcpIds)];
        
        // 删除不再需要的关联
        if (toRemove.Count > 0)
        {
            List<ChatConfigMcp> itemsToRemove = [.. config.ChatConfigMcps.Where(x => toRemove.Contains(x.McpServerId))];
            
            foreach (ChatConfigMcp item in itemsToRemove)
            {
                config.ChatConfigMcps.Remove(item);
            }
        }
        
        // 添加新的关联
        foreach (int mcpServerId in toAdd)
        {
            config.ChatConfigMcps.Add(new ChatConfigMcp
            {
                ChatConfig = config,
                McpServerId = mcpServerId,
                Headers = Mcps[mcpServerId].CustomHeaders,
            });
        }
        
        // 更新现有关联的 Headers（如果有变化）
        foreach (ChatConfigMcp existing in config.ChatConfigMcps.Where(x => toUpdate.Contains(x.McpServerId)))
        {
            string? newHeaders = Mcps[existing.McpServerId].CustomHeaders;
            if (existing.Headers != newHeaders)
            {
                existing.Headers = newHeaders;
            }
        }
    }
}
