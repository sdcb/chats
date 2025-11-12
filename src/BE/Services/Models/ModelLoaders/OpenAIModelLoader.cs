using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using OpenAI;
using OpenAI.Models;
using System.ClientModel;

namespace Chats.BE.Services.Models.ModelLoaders;

public class OpenAIModelLoader : ModelLoader
{
    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(modelKey, []);
        ClientResult<OpenAIModelCollection> result = await api.GetOpenAIModelClient().GetModelsAsync(cancellationToken);
        return [.. result.Value.Select(m => m.Id)];
    }
}
