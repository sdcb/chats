using Chats.DB;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class QwenChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);
        Model model = request.GetRequiredModel();

        if (model.CurrentSnapshot.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["enable_search"] = true;
        }

        if (Model.GetSupportedEffortsAsArray(model.CurrentSnapshot.SupportedEfforts).Length != 0)
        {
            if (ReasoningEfforts.IsLowOrMinimal(request.ChatConfig.Effort))
            {
                body["enable_thinking"] = false;
            }
        }

        return body;
    }
}
