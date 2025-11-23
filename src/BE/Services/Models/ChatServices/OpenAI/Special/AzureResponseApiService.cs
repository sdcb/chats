using Chats.BE.DB;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureResponseApiService(ILogger<AzureResponseApiService> logger) : ResponseApiService(logger)
{
    protected override OpenAIResponseClient CreateResponseAPI(Model model, PipelinePolicy[] pipelinePolicies)
    {
        ModelKey transformedKey = AzureAIFoundryChatService.CreateTransformedModelKey(model.ModelKey);
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(transformedKey, pipelinePolicies);
        return api.GetOpenAIResponseClient(model.DeploymentName);
    }
}