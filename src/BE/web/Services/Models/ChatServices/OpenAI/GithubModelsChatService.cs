using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class GithubModelsChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);
        var model = request.GetRequiredModel();

        if (model.CurrentSnapshot.DeploymentName.Contains("Mistral", StringComparison.OrdinalIgnoreCase))
        {
            // Mistral model does not support user field
            body.Remove("user");
        }

        return body;
    }
}
