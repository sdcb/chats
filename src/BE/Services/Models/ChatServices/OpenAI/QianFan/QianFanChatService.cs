using Chats.BE.DB;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using System.Text.Json;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;

public class QianFanChatService : ChatCompletionService
{
    protected override ChatClient CreateChatClient(Model model, PipelinePolicy[] perCallPolicies)
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
        foreach (PipelinePolicy policy in perCallPolicies)
        {
            oaic.AddPolicy(policy, PipelinePosition.PerCall);
        }
        OpenAIClient api = new(new ApiKeyCredential(cfg.ApiKey), oaic);
        return api.GetChatClient(model.DeploymentName);
    }

    protected override ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = base.ExtractOptions(request);
        if (request.ChatConfig.Model.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            cco.Patch.Set("$.web_search"u8, BinaryData.FromObjectAsJson(new Dictionary<string, object>()
            {
                ["enable"] = true,
                ["enable_citation"] = false,
                ["enable_trace"] = false,
            }));
        }
        return cco;
    }
}
