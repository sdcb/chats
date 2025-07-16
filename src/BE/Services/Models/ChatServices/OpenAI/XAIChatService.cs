using Chats.BE.DB;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class XAIChatService(Model model) : OpenAIChatService(model, new Uri("https://api.x.ai/v1"))
{
    protected override Dtos.ChatTokenUsage GetUsage(ChatTokenUsage usage)
    {
        return new Dtos.ChatTokenUsage
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount + usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
            ReasoningTokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
        };
    }

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        IDictionary<string, BinaryData> said = options.GetOrCreateSerializedAdditionalRawData();
        said["search_parameters"] = BinaryData.FromObjectAsJson(new
        {
            mode = enabled ? "on" : "off", // also supports "auto"
            // return_citations, from_date, to_date, max_search_results, sources is also supported but not used
            // https://docs.x.ai/docs/guides/live-search
        });
    }
}
