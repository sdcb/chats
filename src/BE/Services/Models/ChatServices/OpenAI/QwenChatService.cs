using Chats.BE.DB.Enums;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class QwenChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["enable_search"] = true;
        }

        if (DB.Model.GetReasoningEffortOptionsAsInt32(request.ChatConfig.Model.ReasoningEffortOptions).Length != 0)
        {
            if (request.ChatConfig.ReasoningEffort.IsLowOrMinimal())
            {
                body["enable_thinking"] = false;
            }
        }

        return body;
    }
}
