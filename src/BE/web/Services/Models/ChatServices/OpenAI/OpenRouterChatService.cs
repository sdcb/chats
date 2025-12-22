using Chats.Web.DB;
using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

public class OpenRouterChatService(IHttpClientFactory httpClientFactory, HostUrlService hostUrlService) : ChatCompletionService(httpClientFactory)
{
    protected override void AddAuthorizationHeader(HttpRequestMessage request, ModelKey modelKey)
    {
        base.AddAuthorizationHeader(request, modelKey);
        request.Headers.Add("X-Title", "Sdcb Chats");
        request.Headers.Add("HTTP-Referer", hostUrlService.GetFEUrl());
    }

    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        body["reasoning"] = new JsonObject();
        body["provider"] = new JsonObject { ["sort"] = "throughput" };

        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["plugins"] = new JsonArray
            {
                new JsonObject { ["id"] = "web" }
            };
        }

        return body;
    }
}
