using Chats.BE.DB;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public static class OpenAIHelper
{
    internal static OpenAIClient BuildOpenAIClient(ModelKey modelKey, params PipelinePolicy[] perCallPolicies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        // Fallback logic: ModelKey.Host -> ModelProviderInfo.GetInitialHost
        Uri? endpoint = !string.IsNullOrWhiteSpace(modelKey.Host)
            ? new Uri(modelKey.Host)
            : (ModelProviderInfo.GetInitialHost((DB.Enums.DBModelProvider)modelKey.ModelProviderId) switch
            {
                null => null,
                var x => new Uri(x)
            });

        OpenAIClientOptions oaic = new()
        {
            Endpoint = endpoint,
            NetworkTimeout = ChatService.NetworkTimeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        foreach (PipelinePolicy policy in perCallPolicies)
        {
            oaic.AddPolicy(policy, PipelinePosition.PerCall);
        }
        OpenAIClient api = new(new ApiKeyCredential(modelKey.Secret!), oaic);
        return api;
    }
}
