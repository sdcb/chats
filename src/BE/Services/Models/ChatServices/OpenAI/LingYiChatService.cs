using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;

using OpenAI.Chat;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class LingYiChatService : ChatCompletionService
{
    protected override ChatClient CreateChatClient(Model model, PipelinePolicy[] perCallPolicies)
    {
        List<PipelinePolicy> policies = [.. perCallPolicies];
        policies.Add(new ReplaceSseContentPolicy("\"finish_reason\":\"\"", "\"finish_reason\":null"));
        return base.CreateChatClient(model, [.. policies]);
    }
}
