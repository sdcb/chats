namespace Chats.BE.DB;

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
        config.ReasoningEffort = ChatConfig.ReasoningEffort;
    }

    public ChatSpan ToChatSpan(Model model, byte spanId)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new ChatSpan
        {
            ChatId = model.Id,
            SpanId = spanId,
            Enabled = Enabled,
            ChatConfig = new ChatConfig
            {
                ModelId = ChatConfig.ModelId,
                SystemPrompt = string.IsNullOrEmpty(ChatConfig.SystemPrompt) ? null : ChatConfig.SystemPrompt,
                Temperature = ChatConfig.Temperature,
                WebSearchEnabled = ChatConfig.WebSearchEnabled,
                MaxOutputTokens = ChatConfig.MaxOutputTokens,
                ReasoningEffort = ChatConfig.ReasoningEffort,
            },
        };
    }
}
