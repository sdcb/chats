using Chats.Web.DB;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

public class AzureAIFoundryChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override string GetEndpoint(ModelKey modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DB.Enums.DBModelProvider)modelKey.ModelProviderId);
        }

        return TransformAzureAIFoundryHost(host);
    }

    internal static string TransformAzureAIFoundryHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "";
        }

        // 如果已经以 /openai/v1 或 /openai/v1/ 结尾，不做修改
        if (host.EndsWith("/openai/v1") || host.EndsWith("/openai/v1/"))
        {
            return host.TrimEnd('/');
        }

        // 否则添加 /openai/v1
        return host.TrimEnd('/') + "/openai/v1";
    }

    public static ModelKey CreateTransformedModelKey(ModelKey modelKey)
    {
        return new ModelKey
        {
            Id = modelKey.Id,
            ModelProviderId = modelKey.ModelProviderId,
            Name = modelKey.Name,
            Host = TransformAzureAIFoundryHost(modelKey.Host),
            Secret = modelKey.Secret,
            CreatedAt = modelKey.CreatedAt,
            UpdatedAt = modelKey.UpdatedAt,
            Order = modelKey.Order,
        };
    }
}
