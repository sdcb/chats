using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class XunfeiChatService(Model model) : OpenAIChatService(model, new Uri("https://spark-api-open.xf-yun.com/v1"))
{
    protected override async Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (Model.ModelReference.AllowSearch)
        {
            options.SetWebSearchEnabled_XunfeiStyle(feOptions.WebSearchEnabled);
        }
        return await base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}
