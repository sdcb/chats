using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using Microsoft.OpenApi.Services;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GLMChatService(Model model) : OpenAIChatService(model, new Uri("https://open.bigmodel.cn/api/paas/v4/"))
{
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        // https://bigmodel.cn/dev/howuse/websearch
        options.GetOrCreateSerializedAdditionalRawData()["tools"] = BinaryData.FromObjectAsJson(new[]
        {
            new
            {
                type = "web_search",
                web_search = new
                {
                    enable = enabled,
                },
            }
        });
    }
}
