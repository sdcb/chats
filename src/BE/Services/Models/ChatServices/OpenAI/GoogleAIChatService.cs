using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GoogleAIChatService(Model model) : ChatCompletionService(model)
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        options.EndUserId = null;
        return base.FEPreprocess(messages, options, feOptions, fup, cancellationToken);
    }

    protected override bool SupportsVisionLink => false;
}