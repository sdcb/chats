using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class DoubaoChatService(Model model) : ChatCompletionService(model, new Uri("https://ark.cn-beijing.volces.com/api/v3/"))
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        options.SetMaxTokens(Model.ModelReference.MaxResponseTokens);
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}