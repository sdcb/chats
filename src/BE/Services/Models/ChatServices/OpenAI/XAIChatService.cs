using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class XAIChatService(Model model) : OpenAIChatService(model, new Uri("https://api.x.ai/v1"))
{
    protected override Dtos.ChatTokenUsage GetUsage(global::OpenAI.Chat.ChatTokenUsage usage)
    {
        return new Dtos.ChatTokenUsage
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount + usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
            ReasoningTokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
        };
    }
}
