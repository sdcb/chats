using Chats.BE.DB;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class XAIChatService(Model model) : ChatCompletionService(model)
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

    protected override ChatCompletionOptions ExtractOptions(ChatServiceRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        if (Model.AllowSearch)
        {
            cco.Patch.Set("$.search_parameters"u8, BinaryData.FromObjectAsJson(new
            {
                mode = request.ChatConfig.WebSearchEnabled ? "on" : "off", // also supports "auto"
                // return_citations, from_date, to_date, max_search_results, sources is also supported but not used
                // https://docs.x.ai/docs/guides/live-search
            }));
        }
        return cco;
    }
}
