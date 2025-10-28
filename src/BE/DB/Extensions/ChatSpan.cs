using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class ChatSpan
{
    public ChatCompletionOptions ToChatCompletionOptions(int userId, ChatSpan span, UserModel userModel)
    {
        ChatCompletionOptions cco = new()
        {
            Temperature = span.ChatConfig.Temperature,
            EndUserId = userId.ToString(),
        };
        if (span.ChatConfig.MaxOutputTokens.HasValue)
        {
            cco.SetMaxTokens(span.ChatConfig.MaxOutputTokens.Value, userModel.Model.UseMaxCompletionTokens);
        }
        return cco;
    }

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
