using Chats.BE.DB;
using OpenAI;
using OpenAI.Images;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureImageGenerationService(Model model) : ImageGenerationService(model, CreateAzureImageClient(model))
{
    private static ImageClient CreateAzureImageClient(Model model)
    {
        ModelKey transformedKey = AzureAIFoundryChatService.CreateTransformedModelKey(model.ModelKey);
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(transformedKey, []);
        return api.GetImageClient(model.DeploymentName);
    }
}
