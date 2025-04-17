using Azure.AI.OpenAI;
using Chats.BE.DB;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Reflection;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class AzureChatService(Model model) : OpenAIChatService(model, CreateChatClient(model))
{
    static ChatClient CreateChatClient(Model model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Host, nameof(model.ModelKey.Host));
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        AzureOpenAIClientOptions options = new();
        if (ModelReference.IsSdkUnsupportedO1(model.ModelReference.Name))
        {
            // o1 only supports api version: 2024-12-01-preview
            options
                .GetType()
                .GetField("<Version>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                !.SetValue(options, "2024-12-01-preview");
        }
        
        OpenAIClient api = new AzureOpenAIClient(
            new Uri(model.ModelKey.Host), 
            new ApiKeyCredential(model.ModelKey.Secret), options);
        return api.GetChatClient(model.ApiModelId);
    }
}
