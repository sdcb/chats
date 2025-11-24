using Chats.BE.DB;
using OpenAI;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureImageGenerationService : ImageGenerationService
{
    protected override OpenAIClient CreateOpenAIClient(ModelKey modelKey, params PipelinePolicy[] perCallPolicies)
    {
        ModelKey transformedKey = AzureAIFoundryChatService.CreateTransformedModelKey(modelKey);
        return base.CreateOpenAIClient(transformedKey, perCallPolicies);
    }
}
