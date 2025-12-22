using Chats.Web.DB;

namespace Chats.Web.Services.Models.ChatServices.OpenAI.Special;

public class AzureResponseApiService(IHttpClientFactory httpClientFactory, ILogger<AzureResponseApiService> logger) : ResponseApiService(httpClientFactory, logger)
{
    protected override string GetEndpoint(ModelKey modelKey)
    {
        // Response API uses /v1/responses, so we only need to add /openai prefix
        // Result: {host}/openai/v1/responses
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DB.Enums.DBModelProvider)modelKey.ModelProviderId);
        }
        return TransformAzureHost(host);
    }

    private static string TransformAzureHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "";
        }

        // For Response API: host + /openai (then ResponseApiService adds /v1/responses)
        // If already ends with /openai or /openai/v1, handle accordingly
        if (host.EndsWith("/openai/v1") || host.EndsWith("/openai/v1/"))
        {
            // Remove the /v1 part since ResponseApiService will add /v1/responses
            return host.TrimEnd('/')[..^3]; // Remove "/v1"
        }
        if (host.EndsWith("/openai") || host.EndsWith("/openai/"))
        {
            return host.TrimEnd('/');
        }

        return host.TrimEnd('/') + "/openai";
    }
}
