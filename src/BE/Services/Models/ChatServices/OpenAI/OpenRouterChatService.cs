using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class OpenRouterChatService(Model model, HostUrlService hostUrlService) : ChatCompletionService(model, new Uri("https://openrouter.ai/api/v1"),
    [
        new AddHeaderPolicy("X-Title", "Sdcb Chats"), 
        new AddHeaderPolicy("HTTP-Referer", hostUrlService.GetFEUrl())
    ])
{
    protected override ReadOnlySpan<byte> ReasoningEffortPropName => "$.reasoning"u8;

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        if (enabled)
        {
            options.Patch.Set("$.plugins"u8, BinaryData.FromObjectAsJson(new[]
            {
                new { id = "web" }
            }));
        }
    }

    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        options.Patch.Set("$.reasoning"u8, BinaryData.FromObjectAsJson(new { }));
        options.Patch.Set("$.provider"u8, BinaryData.FromObjectAsJson(new { sort = "throughput" }));
        return base.FEPreprocess(messages, options, feOptions, fup, cancellationToken);
    }
}
