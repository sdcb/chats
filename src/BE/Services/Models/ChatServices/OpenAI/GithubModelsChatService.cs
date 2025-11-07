using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GithubModelsChatService(Model model) : ChatCompletionService(model)
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        if (Model.Name.Contains("Mistral", StringComparison.OrdinalIgnoreCase))
        {
            // Mistral model does not support end-user ID
            options.EndUserId = null;
        }
        return base.FEPreprocess(messages, options, feOptions, fup, cancellationToken);
    }
}