using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class ChatSpan
{
    public ChatCompletionOptions ToChatCompletionOptions(int userId, ChatSpan span)
    {
        ChatCompletionOptions cco = new()
        {
            Temperature = span.ChatConfig.Temperature,
            EndUserId = userId.ToString(),
        };
        return cco;
    }
}
