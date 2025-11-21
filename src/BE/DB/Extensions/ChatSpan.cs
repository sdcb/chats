namespace Chats.BE.DB;

public partial class ChatSpan
{
    public ChatSpan Clone()
    {
        return new ChatSpan
        {
            ChatId = ChatId,
            SpanId = SpanId,
            Enabled = Enabled,
            ChatConfig = ChatConfig.Clone(),
        };
    }
}
