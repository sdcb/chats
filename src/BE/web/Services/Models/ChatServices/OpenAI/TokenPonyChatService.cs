using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

public class TokenPonyChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        // TokenPony 的 deepseek-v3.2 模型需要通过 chat_template_kwargs 传递 thinking 参数
        if (request.ChatConfig.ThinkingBudget.HasValue && request.ChatConfig.Model.DeploymentName.StartsWith("deepseek-v3."))
        {
            body["chat_template_kwargs"] = new JsonObject
            {
                ["thinking"] = true
            };
        }

        return body;
    }
}
