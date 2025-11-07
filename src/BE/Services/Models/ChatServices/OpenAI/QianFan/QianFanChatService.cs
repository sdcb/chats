using Chats.BE.DB;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using System.Text.Json;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;

public class QianFanChatService(Model model) : ChatCompletionService(model, CreateChatClient(model))
{
    private static ChatClient CreateChatClient(Model model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));
        
        // Fallback logic: ModelKey.Host -> ModelProviderInfo.GetInitialHost
        Uri? endpoint = !string.IsNullOrWhiteSpace(model.ModelKey.Host) 
            ? new Uri(model.ModelKey.Host) 
            : (ModelProviderInfo.GetInitialHost((DB.Enums.DBModelProvider)model.ModelKey.ModelProviderId) switch
                {
                    null => null,
                    var x => new Uri(x)
                });
        
        OpenAIClientOptions oaic = new()
        {
            Endpoint = endpoint,
        };

        JsonQianFanApiConfig? cfg = JsonSerializer.Deserialize<JsonQianFanApiConfig>(model.ModelKey.Secret)
            ?? throw new ArgumentException("Invalid qianfan secret");

        oaic.AddPolicy(new AddHeaderPolicy("appid", cfg.AppId), PipelinePosition.PerCall);
        oaic.AddPolicy(new ReplaceSseContentPolicy("\"finish_reason\":\"normal\"", "\"finish_reason\":null"), PipelinePosition.PerCall);
        OpenAIClient api = new(new ApiKeyCredential(cfg.ApiKey), oaic);
        return api.GetChatClient(model.DeploymentName);
    }

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        options.Patch.Set("$.web_search"u8, BinaryData.FromObjectAsJson(new Dictionary<string, object>()
        {
            ["enable"] = enabled,
            ["enable_citation"] = false,
            ["enable_trace"] = false,
        }));
    }
}
