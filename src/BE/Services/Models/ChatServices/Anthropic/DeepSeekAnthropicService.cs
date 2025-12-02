using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

public class MiniMaxAnthropicService(IHttpClientFactory httpClientFactory) : AnthropicChatService(httpClientFactory)
{
    protected override (string url, string apiKey) GetEndpointAndKey(ModelKey modelKey)
    {
        return (modelKey.Host ?? "https://api.minimaxi.com/anthropic", modelKey.Secret ?? throw new ArgumentNullException(nameof(modelKey), "ModelKey.Secret cannot be null for DeepSeekAnthropicService"));
    }
}
