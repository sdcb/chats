using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GLMChatService(Model model) : ChatCompletionService(model)
{
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        // https://bigmodel.cn/dev/howuse/websearch
        options.Patch.Set("$.tools"u8, BinaryData.FromObjectAsJson(new[]
        {
            new
            {
                type = "web_search",
                web_search = new
                {
                    enable = enabled,
                },
            }
        }));
    }
}
