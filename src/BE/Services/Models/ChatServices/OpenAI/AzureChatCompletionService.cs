using Chats.BE.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class AzureChatCompletionService(Model model) : ChatCompletionService(model, HostTransform(model.ModelKey))
{
    internal static Uri? HostTransform(ModelKey key)
    {
        if (string.IsNullOrWhiteSpace(key.Host))
        {
            return null;
        }

        if (key.Host.EndsWith("/openai/v1?api-version=preview"))
        {
            return null;
        }

        key.Host = key.Host.TrimEnd('/') + "/openai/v1?api-version=preview";
        return null;
    }
}
