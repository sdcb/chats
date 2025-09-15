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
            if (userModel.Model.ModelReference.ProviderId == (int)DBModelProvider.AzureOpenAI || userModel.Model.ModelReference.ProviderId == (int)DBModelProvider.OpenAI)
            {
                cco.MaxOutputTokenCount = span.ChatConfig.MaxOutputTokens.Value;
            }
            else
            {
                cco.GetOrCreateSerializedAdditionalRawData()["max_tokens"] = BinaryData.FromObjectAsJson(span.ChatConfig.MaxOutputTokens.Value);
            }
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
