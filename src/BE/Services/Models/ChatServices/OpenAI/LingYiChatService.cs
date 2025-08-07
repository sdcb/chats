using Chats.BE.DB;
using OpenAI.Chat;
using Chats.BE.Services.Models.Extensions;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class LingYiChatService(Model model) : ChatCompletionService(model, new Uri("https://api.lingyiwanwu.com/v1"), 
    new ReplaceSseContentPolicy("\"finish_reason\":\"\"", "\"finish_reason\":null"))
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (Model.ModelReference.Name == "yi-lightning")
        {
            options.SetMaxTokens(Model.ModelReference.MaxResponseTokens);
        }
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}
