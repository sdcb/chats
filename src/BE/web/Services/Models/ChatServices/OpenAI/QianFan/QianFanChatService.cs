using Chats.Web.Controllers.Chats.Chats;
using Chats.Web.DB;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI.QianFan;

public class QianFanChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override void AddAuthorizationHeader(HttpRequestMessage request, ModelKey modelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        JsonQianFanApiConfig? cfg = JsonSerializer.Deserialize<JsonQianFanApiConfig>(modelKey.Secret)
            ?? throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "Invalid qianfan secret");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);
        request.Headers.Add("appid", cfg.AppId);
    }

    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            body["web_search"] = new JsonObject
            {
                ["enable"] = true,
                ["enable_citation"] = false,
                ["enable_trace"] = false
            };
        }

        return body;
    }
}
