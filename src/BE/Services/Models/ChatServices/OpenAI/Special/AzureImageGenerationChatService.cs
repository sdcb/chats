using Azure.AI.OpenAI;
using OpenAI;
using System.ClientModel;
using System.Reflection;
using Chats.BE.DB;
using OpenAI.Images;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureImageGenerationChatService(Model model) : ImageGenerationChatService(model)
{
    protected override ImageClient CreateImageGenerationAPI(Model model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Host, nameof(model.ModelKey.Host));
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        AzureOpenAIClientOptions options = new()
        {
            NetworkTimeout = NetworkTimeout,
        };

        OpenAIClient api = new AzureOpenAIClient(
            new Uri(model.ModelKey.Host),
            new ApiKeyCredential(model.ModelKey.Secret), options);
        ImageClient cc = api.GetImageClient(model.ApiModelId);
        SetApiVersion(cc, "2025-04-01-preview");
        return cc;

        static void SetApiVersion(ImageClient api, string version)
        {
            FieldInfo? versionField = api.GetType().GetField("_apiVersion", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Unable to access the API version field.");
            versionField.SetValue(api, version);
        }
    }
}
