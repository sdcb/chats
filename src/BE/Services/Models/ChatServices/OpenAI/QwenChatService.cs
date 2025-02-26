using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class QwenChatService(Model model) : OpenAIChatService(model, new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"))
{
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        options.GetOrCreateSerializedAdditionalRawData()["enable_search"] = BinaryData.FromObjectAsJson(enabled);
    }
}
