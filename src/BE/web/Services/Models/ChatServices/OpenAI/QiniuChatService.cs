using Chats.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class QiniuChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    private static readonly string[] DefaultModels = ["deepseek-v3"];

    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        try
        {
            string[] models = await base.ListModels(modelKey, cancellationToken);
            return models.Length > 0 ? models : DefaultModels;
        }
        catch
        {
            return DefaultModels;
        }
    }
}
