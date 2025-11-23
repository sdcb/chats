using Chats.BE.DB;
using OpenAI;
using OpenAI.Images;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureImageGenerationService : ImageGenerationService
{
    protected override ImageClient CreateImageGenerationAPI(Model model, PipelinePolicy[] perCallPolicies)
    {
        ModelKey transformedKey = AzureAIFoundryChatService.CreateTransformedModelKey(model.ModelKey);
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(transformedKey, perCallPolicies);
        return api.GetImageClient(model.DeploymentName);
    }
}
