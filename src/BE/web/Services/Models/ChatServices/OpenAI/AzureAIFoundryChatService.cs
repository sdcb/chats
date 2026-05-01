using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class AzureAIFoundryChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override string GetEndpoint(ModelKeySnapshot modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DBModelProvider)modelKey.ModelProviderId);
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

    public static ModelKeySnapshot CreateTransformedModelKey(ModelKeySnapshot modelKey)
    {
        return new ModelKeySnapshot
        {
            Id = modelKey.Id,
            ModelKeyId = modelKey.ModelKeyId,
            ModelProviderId = modelKey.ModelProviderId,
            Name = modelKey.Name,
            Host = TransformAzureAIFoundryHost(modelKey.Host),
            Secret = modelKey.Secret,
            CreatedAt = modelKey.CreatedAt,
        };
    }
}
