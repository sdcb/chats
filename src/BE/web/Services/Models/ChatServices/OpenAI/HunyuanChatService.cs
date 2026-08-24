using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class HunyuanChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);
        var model = request.GetRequiredModel();

        if (model.CurrentSnapshot.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["enable_enhancement"] = true;
            body["force_search_enhancement"] = true;
        }

        return body;
    }
}
