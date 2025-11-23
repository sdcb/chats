using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GoogleAIChatService : ChatCompletionService
{
    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        cco.EndUserId = null;
        return cco;
    }

    protected override bool SupportsVisionLink => false;
}