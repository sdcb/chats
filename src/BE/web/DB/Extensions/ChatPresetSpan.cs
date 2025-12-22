namespace Chats.Web.DB;

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
        config.ReasoningEffortId = ChatConfig.ReasoningEffortId;
        config.ImageSize = ChatConfig.ImageSize;
        config.ThinkingBudget = ChatConfig.ThinkingBudget;

        // Update ChatConfigMcp associations
        HashSet<int> existingMcpIds = [.. config.ChatConfigMcps.Select(x => x.McpServerId)];
        HashSet<int> newMcpIds = [.. ChatConfig.ChatConfigMcps.Select(x => x.McpServerId)];

        // Remove associations that are no longer needed
        List<ChatConfigMcp> toRemove = [.. config.ChatConfigMcps.Where(x => !newMcpIds.Contains(x.McpServerId))];
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
            ReasoningEffortId = ChatConfig.ReasoningEffortId,
            ImageSize = ChatConfig.ImageSize,
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
                McpServerId = mcpAssoc.McpServerId
            });
        }

        return chatSpan;
    }
}
