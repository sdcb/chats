using Chats.BE.DB;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class AzureAIFoundryChatService : ChatCompletionService
{
    protected override ChatClient CreateChatClient(Model model, PipelinePolicy[] perCallPolicies)
    {
        OpenAIClient api = CreateAzureAIFoundryOpenAIClient(model.ModelKey, perCallPolicies);
        return api.GetChatClient(model.DeploymentName);
    }

    private static OpenAIClient CreateAzureAIFoundryOpenAIClient(ModelKey modelKey, PipelinePolicy[] perCallPolicies)
    {
        ModelKey transformedKey = CreateTransformedModelKey(modelKey);
        return ChatCompletionService.CreateOpenAIClient(transformedKey, perCallPolicies);
    }

    internal static ModelKey CreateTransformedModelKey(ModelKey modelKey)
    {
        // 对于 Azure AI Foundry，先进行 Host 转换，确保 URL 以 /openai/v1/ 结尾
        string? transformedHost = TransformAzureAIFoundryHost(modelKey.Host) 
            ?? ModelProviderInfo.GetInitialHost((DB.Enums.DBModelProvider)modelKey.ModelProviderId);
        
        // 创建一个新的 ModelKey 副本，使用转换后的 Host
        return new ModelKey()
        {
            Id = modelKey.Id,
            ModelProviderId = modelKey.ModelProviderId,
            Name = modelKey.Name,
            Host = transformedHost,
            Secret = modelKey.Secret,
            CreatedAt = modelKey.CreatedAt,
            UpdatedAt = modelKey.UpdatedAt,
            Order = modelKey.Order
        };
    }

    internal static Uri? HostTransform(ModelKey key)
    {
        if (string.IsNullOrWhiteSpace(key.Host))
        {
            return null;
        }

        string transformed = TransformAzureAIFoundryHost(key.Host) ?? key.Host;
        return new Uri(transformed);
    }

    internal static string? TransformAzureAIFoundryHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        // 如果已经以 /openai/v1 或 /openai/v1/ 结尾，不做修改
        if (host.EndsWith("/openai/v1") || host.EndsWith("/openai/v1/"))
        {
            return host;
        }

        // 否则添加 /openai/v1/
        return host.TrimEnd('/') + "/openai/v1/";
    }
}
