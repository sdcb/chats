using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using OpenAI.Chat;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class OpenRouterChatService(HostUrlService hostUrlService) : ChatCompletionService
{
    protected override ChatClient CreateChatClient(Model model, PipelinePolicy[] perCallPolicies)
    {
        List<PipelinePolicy> policies = [.. perCallPolicies];
        policies.Add(new AddHeaderPolicy("X-Title", "Sdcb Chats"));
        policies.Add(new AddHeaderPolicy("HTTP-Referer", hostUrlService.GetFEUrl()));
        
        return base.CreateChatClient(model, [.. policies]);
    }

    protected override ReadOnlySpan<byte> ReasoningEffortPropName => "$.reasoning"u8;

    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        cco.Patch.Set("$.reasoning"u8, BinaryData.FromObjectAsJson(new { }));
        cco.Patch.Set("$.provider"u8, BinaryData.FromObjectAsJson(new { sort = "throughput" }));

        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            cco.Patch.Set("$.plugins"u8, BinaryData.FromObjectAsJson(new[]
            {
                new { id = "web" }
            }));
        }

        return cco;
    }
}
