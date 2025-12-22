using Chats.Web.DB;

namespace Chats.Web.Services.Models.ChatServices.OpenAI.Special;

public class AzureImageGenerationService(IHttpClientFactory httpClientFactory) : ImageGenerationService(httpClientFactory)
{
    protected override string GetEndpoint(ModelKey modelKey)
    {
        // Image API uses /v1/images/generations, so we only need to add /openai prefix
        // Result: {host}/openai/v1/images/generations
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

        // For Image API: host + /openai (then ImageGenerationService adds /v1/images/...)
        if (host.EndsWith("/openai/v1") || host.EndsWith("/openai/v1/"))
        {
            return host.TrimEnd('/')[..^3]; // Remove "/v1"
        }
        if (host.EndsWith("/openai") || host.EndsWith("/openai/"))
        {
            return host.TrimEnd('/');
        }

        return host.TrimEnd('/') + "/openai";
    }
}
