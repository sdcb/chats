using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GithubModelsChatService(Model model) : ChatCompletionService(model, new Uri("https://models.inference.ai.azure.com"))
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (Model.ModelReference.DisplayName == "Mistral")
        {
            // Mistral model does not support end-user ID
            options.EndUserId = null;
        }
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}