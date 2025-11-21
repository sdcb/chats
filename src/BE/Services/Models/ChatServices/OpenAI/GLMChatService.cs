using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GLMChatService(Model model) : ChatCompletionService(model)
{
    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        // https://bigmodel.cn/dev/howuse/websearch
        if (request.ChatConfig.WebSearchEnabled)
        {
            cco.Patch.Set("$.tools"u8, BinaryData.FromObjectAsJson(new[]
            {
                new
                {
                    type = "web_search",
                    web_search = new
                    {
                        enable = true,
                    },
                }
            }));
        }
        return cco;
    }
}
