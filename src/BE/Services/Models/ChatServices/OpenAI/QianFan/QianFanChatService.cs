using Chats.BE.DB;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using System.Text.Json;
using Chats.BE.Services.Models.Extensions;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;

public class QianFanChatService(Model model) : ChatCompletionService(model, CreateChatClient(model, new Uri("https://qianfan.baidubce.com/v2")))
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

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        options.GetOrCreateSerializedAdditionalRawData()["web_search"] = BinaryData.FromObjectAsJson(new Dictionary<string, object>()
        {
            ["enable"] = enabled,
            ["enable_citation"] = false,
            ["enable_trace"] = false,
        });
    }
}
