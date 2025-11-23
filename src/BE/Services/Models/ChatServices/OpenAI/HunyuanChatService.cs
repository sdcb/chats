using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService : ChatCompletionService
{
    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            cco.Patch.Set("$.enable_enhancement"u8, true);
            cco.Patch.Set("$.force_search_enhancement"u8, true);
        }
        return cco;
    }
}