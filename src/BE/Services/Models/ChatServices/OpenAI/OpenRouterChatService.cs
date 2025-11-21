using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class OpenRouterChatService(Model model, HostUrlService hostUrlService) : ChatCompletionService(model,
    [
        new AddHeaderPolicy("X-Title", "Sdcb Chats"), 
        new AddHeaderPolicy("HTTP-Referer", hostUrlService.GetFEUrl())
    ])
{
    protected override ReadOnlySpan<byte> ReasoningEffortPropName => "$.reasoning"u8;

    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        cco.Patch.Set("$.reasoning"u8, BinaryData.FromObjectAsJson(new { }));
        cco.Patch.Set("$.provider"u8, BinaryData.FromObjectAsJson(new { sort = "throughput" }));

        if (Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            cco.Patch.Set("$.plugins"u8, BinaryData.FromObjectAsJson(new[]
            {
                new { id = "web" }
            }));
        }

        return cco;
    }
}
