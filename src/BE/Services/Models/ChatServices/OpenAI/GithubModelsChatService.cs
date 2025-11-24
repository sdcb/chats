using Chats.BE.DB;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GithubModelsChatService : ChatCompletionService
{
    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions options = base.ExtractOptions(request);
        if (request.ChatConfig.Model.DeploymentName.Contains("Mistral", StringComparison.OrdinalIgnoreCase))
        {
            // Mistral model does not support tool calls
            options.EndUserId = null;
        }
        return options;
    }
}