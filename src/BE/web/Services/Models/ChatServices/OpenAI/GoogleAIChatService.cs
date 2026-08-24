using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// Google AI Chat Service using OpenAI-compatible API
/// </summary>
public class GoogleAIChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);
        var model = request.GetRequiredModel();

        if (model.CurrentSnapshot.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["google_search"] = new JsonObject()
                }
            };
        }

        if (request.ChatConfig.CodeExecutionEnabled)
        {
            body["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["code_execution"] = new JsonObject()
                }
            };
        }

        return body;
    }
}
