using Chats.DB;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

public class DeepSeekAnthropicService(IHttpClientFactory httpClientFactory) : AnthropicChatService(httpClientFactory)
{
    protected override (string url, string apiKey) GetEndpointAndKey(ModelKey modelKey)
    {
        return (modelKey.Host ?? "https://api.deepseek.com/anthropic", modelKey.Secret ?? throw new ArgumentNullException(nameof(modelKey), "ModelKey.Secret cannot be null for DeepSeekAnthropicService"));
    }
}
