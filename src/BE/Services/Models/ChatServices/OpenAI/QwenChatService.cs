using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class QwenChatService(Model model) : OpenAIChatService(model, new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"))
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (Model.ModelReference.AllowSearch)
        {
            options.SetWebSearchEnabled_QwenStyle(feOptions.WebSearchEnabled);
        }
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}
