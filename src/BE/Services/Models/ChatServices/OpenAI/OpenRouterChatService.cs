using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using Chats.BE.Services.Models.ChatServices.OpenAI.ReasoningContents;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class OpenRouterChatService(Model model, HostUrlService hostUrlService) : OpenAIChatService(model, new Uri("https://openrouter.ai/api/v1"),
    [
        new AddHeaderPolicy("X-Title", "Sdcb Chats"), 
        new AddHeaderPolicy("HTTP-Referer", hostUrlService.GetFEUrl())
    ])
{
    static Func<ChatCompletion, string?> ReasoningContentAccessor { get; } = ReasoningContentFactory.CreateReasoningContentAccessor("reasoning");
    static Func<StreamingChatCompletionUpdate, string?> StreamingReasoningContentAccessor { get; } = ReasoningContentFactory.CreateStreamingReasoningContentAccessor("reasoning");

    protected override string? GetReasoningContent(ChatCompletion delta) => ReasoningContentAccessor(delta);
    protected override string? GetReasoningContent(StreamingChatCompletionUpdate delta) => StreamingReasoningContentAccessor(delta);

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        if (enabled)
        {
            options.GetOrCreateSerializedAdditionalRawData()["plugins"] = BinaryData.FromObjectAsJson(new[]
            {
                new { id = "web" }
            });
        }
    }

    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        IDictionary<string, BinaryData> sard = options.GetOrCreateSerializedAdditionalRawData();
        sard["reasoning"] = BinaryData.FromObjectAsJson(new { });
        sard["provider"] = BinaryData.FromObjectAsJson(new { sort = "throughput" });
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}
