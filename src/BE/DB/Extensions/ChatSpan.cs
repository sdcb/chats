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
        if (span.ChatConfig.ReasoningEffort.HasValue && userModel.Model.ModelReference.SupportReasoningEffort)
        {
            cco.GetOrCreateSerializedAdditionalRawData()["reasoning_effort"] = BinaryData.FromObjectAsJson((DBReasoningEffort)span.ChatConfig.ReasoningEffort.Value switch
            {
                DBReasoningEffort.Low => "low",
                DBReasoningEffort.Medium => "medium",
                DBReasoningEffort.High => "high",
                _ => throw new ArgumentOutOfRangeException(nameof(span.ChatConfig.ReasoningEffort), span.ChatConfig.ReasoningEffort.Value, null),
            });
        }
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
            ChatConfigId = ChatConfigId,
            ChatConfig = ChatConfig.Clone(),
        };
    }
}
