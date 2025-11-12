using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService(Model model) : ChatCompletionService(model)
{
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        options.Patch.Set("$.enable_enhancement"u8, enabled);
        options.Patch.Set("$.force_search_enhancement"u8, enabled);
    }
}