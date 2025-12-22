using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["enable_enhancement"] = true;
            body["force_search_enhancement"] = true;
        }

        return body;
    }
}
