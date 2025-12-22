using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

/// <summary>
/// Google AI Chat Service using OpenAI-compatible API
/// </summary>
public class GoogleAIChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
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
