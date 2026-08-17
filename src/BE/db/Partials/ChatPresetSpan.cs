namespace Chats.DB;

public partial class ChatPresetSpan
{
    public void ApplyTo(ChatSpan span, Model model)
    {
        if (ChatConfig == null)
        {
            throw new InvalidOperationException("ChatConfig is null");
        }

        ArgumentNullException.ThrowIfNull(model);

        span.Enabled = Enabled;

        ChatConfig config = span.ChatConfig ?? throw new InvalidOperationException("ChatSpan.ChatConfig is null");
        config.ModelId = ChatConfig.ModelId;
        config.SystemPrompt = string.IsNullOrEmpty(ChatConfig.SystemPrompt) ? null : ChatConfig.SystemPrompt;
        config.Temperature = ChatConfig.Temperature;
        config.WebSearchEnabled = ChatConfig.WebSearchEnabled;
        config.MaxOutputTokens = ChatConfig.MaxOutputTokens;
        config.Effort = ChatConfig.Effort;
        config.ImageSize = ChatConfig.ImageSize;
        config.Format = ChatConfig.Format;
        config.Compression = ChatConfig.Compression;
        config.ThinkingBudget = ChatConfig.ThinkingBudget;

        Dictionary<int, ChatConfigMcp> desiredMcps = ChatConfig.ChatConfigMcps
            .GroupBy(x => x.McpServerId)
            .ToDictionary(x => x.Key, x => x.First());
        HashSet<int> retainedMcpIds = [];

        foreach (ChatConfigMcp existing in config.ChatConfigMcps.ToArray())
        {
            if (!desiredMcps.TryGetValue(existing.McpServerId, out ChatConfigMcp? desired) ||
                !retainedMcpIds.Add(existing.McpServerId))
            {
                config.ChatConfigMcps.Remove(existing);
                continue;
            }

            existing.CustomHeaders = desired.CustomHeaders;
        }

        foreach ((int mcpServerId, ChatConfigMcp desired) in desiredMcps)
        {
            if (retainedMcpIds.Contains(mcpServerId))
            {
                continue;
            }

            config.ChatConfigMcps.Add(new ChatConfigMcp
            {
                ChatConfig = config,
                McpServerId = mcpServerId,
                CustomHeaders = desired.CustomHeaders,
            });
        }
    }

    public ChatSpan ToChatSpan(Model model, byte spanId)
    {
        ArgumentNullException.ThrowIfNull(model);

        ChatConfig chatConfig = new()
        {
            ModelId = ChatConfig.ModelId,
            SystemPrompt = string.IsNullOrEmpty(ChatConfig.SystemPrompt) ? null : ChatConfig.SystemPrompt,
            Temperature = ChatConfig.Temperature,
            WebSearchEnabled = ChatConfig.WebSearchEnabled,
            MaxOutputTokens = ChatConfig.MaxOutputTokens,
            Effort = ChatConfig.Effort,
            ImageSize = ChatConfig.ImageSize,
            Format = ChatConfig.Format,
            Compression = ChatConfig.Compression,
            ThinkingBudget = ChatConfig.ThinkingBudget,
        };

        ChatSpan chatSpan = new()
        {
            ChatId = model.Id,
            SpanId = spanId,
            Enabled = Enabled,
            ChatConfig = chatConfig,
        };

        // Add ChatConfigMcp associations
        foreach (ChatConfigMcp mcpAssoc in ChatConfig.ChatConfigMcps)
        {
            chatConfig.ChatConfigMcps.Add(new ChatConfigMcp
            {
                ChatConfig = chatConfig,
                McpServerId = mcpAssoc.McpServerId,
                CustomHeaders = mcpAssoc.CustomHeaders,
            });
        }

        return chatSpan;
    }
}
