using Chats.BE.DB;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using System.Text.Json;
using Chats.BE.Services.Models.Extensions;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;

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

    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        if (feOptions.WebSearchEnabled && Model.ModelReference.AllowSearch)
        {
            options.SetWebSearchEnabled_QianFanStyle(true);
        }
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }
}
