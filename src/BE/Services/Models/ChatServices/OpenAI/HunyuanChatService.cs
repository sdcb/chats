using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService(Model model) : ChatCompletionService(model, new Uri("https://api.hunyuan.cloud.tencent.com/v1"))
{
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        options.Patch.Set("$.enable_enhancement"u8, enabled);
        options.Patch.Set("$.force_search_enhancement"u8, enabled);
    }
}