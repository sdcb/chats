using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using System.Text.Json;

namespace Chats.BE.Services.Models.ChatServices.QianFan;

public class QianFanChatService(Model model) : OpenAIChatService(model, CreateChatClient(model, new Uri("https://qianfan.baidubce.com/v2")))
{
    private static ChatClient CreateChatClient(Model model, Uri? suggestedApiUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));
        OpenAIClientOptions oaic = new()
        {
            Endpoint = !string.IsNullOrWhiteSpace(model.ModelKey.Host) ? new Uri(model.ModelKey.Host) : suggestedApiUrl,
        };

        JsonQianFanApiConfig? cfg = JsonSerializer.Deserialize<JsonQianFanApiConfig>(model.ModelKey.Secret)
            ?? throw new ArgumentException("Invalid qianfan secret");

        oaic.AddPolicy(new AddHeaderPolicy("appid", cfg.AppId), PipelinePosition.PerCall);
        oaic.AddPolicy(new ReplaceSseContentPolicy("\"finish_reason\":\"normal\"", "\"finish_reason\":null"), PipelinePosition.PerCall);
        OpenAIClient api = new(new ApiKeyCredential(cfg.ApiKey), oaic);
        return api.GetChatClient(model.ApiModelId);
    }
}
