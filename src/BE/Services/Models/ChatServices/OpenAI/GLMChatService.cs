using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GLMChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        // https://bigmodel.cn/dev/howuse/websearch
        if (request.ChatConfig.WebSearchEnabled)
        {
            body["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "web_search",
                    ["web_search"] = new JsonObject
                    {
                        ["enable"] = true
                    }
                }
            };
        }

        return body;
    }
}
