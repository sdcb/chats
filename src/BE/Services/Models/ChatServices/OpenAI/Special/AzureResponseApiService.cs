using Chats.BE.DB;
using OpenAI;
using OpenAI.Responses;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureResponseApiService(Model model, ILogger logger) : ResponseApiService(model, logger, CreateAzureResponseClient(model))
{
    private static OpenAIResponseClient CreateAzureResponseClient(Model model)
    {
        ModelKey transformedKey = AzureAIFoundryChatService.CreateTransformedModelKey(model.ModelKey);
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(transformedKey, []);
        return api.GetOpenAIResponseClient(model.DeploymentName);
    }
}