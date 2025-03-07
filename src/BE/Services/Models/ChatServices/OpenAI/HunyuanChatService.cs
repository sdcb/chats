using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService(Model model) : OpenAIChatService(model, new Uri("https://api.hunyuan.cloud.tencent.com/v1"))
{
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        IDictionary<string, BinaryData> dict = options.GetOrCreateSerializedAdditionalRawData();
        dict["enable_enhancement"] = BinaryData.FromObjectAsJson(enabled);
        dict["force_search_enhancement"] = BinaryData.FromObjectAsJson(enabled);
    }
}