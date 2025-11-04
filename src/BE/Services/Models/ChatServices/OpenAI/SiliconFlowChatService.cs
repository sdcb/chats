using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class SiliconFlowChatService(Model model) : ChatCompletionService(model, new Uri("https://api.siliconflow.cn/v1"))
{
    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        options.SetMaxTokens(Model.MaxResponseTokens, Model.UseMaxCompletionTokens); // https://api-docs.deepseek.com/zh-cn/quick_start/pricing default 4096 but max 8192
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        if (reasoningEffort.IsLowOrMinimal())
        {
            options.Patch.Set("$.enable_thinking"u8, false);
        }
    }
}