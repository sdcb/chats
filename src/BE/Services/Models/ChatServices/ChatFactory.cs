using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.GoogleAI;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;
using Chats.BE.Services.Models.ChatServices.OpenAI.Special;
using Chats.BE.Services.Models.ChatServices.Test;
using Chats.BE.Services.Models.ModelLoaders;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices;

public class ChatFactory(ILogger<ChatFactory> logger, HostUrlService hostUrlService)
{
    public ChatService CreateChatService(Model model)
    {
        DBModelProvider modelProvider = (DBModelProvider)model.ModelKey.ModelProviderId;
        ChatService cs = modelProvider switch
        {
            DBModelProvider.Test => new TestChatService(model),
            DBModelProvider.OpenAI => model.ModelReference.Name switch
            {
                "gpt-image-1" => new ImageGenerationChatService(model),
                _ => new OpenAIChatService(model),
            },
            DBModelProvider.AzureOpenAI => model.ModelReference.Name switch
            {
                "o3" or "o4-mini" => new AzureResponseApiService(model), 
                "gpt-image-1" => new AzureImageGenerationChatService(model), 
                _ => new AzureChatService(model),
            },
            DBModelProvider.WenXinQianFan => new QianFanChatService(model),
            DBModelProvider.AliyunDashscope => new QwenChatService(model),
            DBModelProvider.ZhiPuAI => new GLMChatService(model),
            DBModelProvider.Moonshot => new KimiChatService(model),
            DBModelProvider.HunYuan => new HunyuanChatService(model),
            DBModelProvider.Sparkdesk => new XunfeiChatService(model),
            DBModelProvider.LingYi => new LingYiChatService(model),
            DBModelProvider.DeepSeek => new DeepSeekChatService(model),
            DBModelProvider.xAI => new XAIChatService(model),
            DBModelProvider.GithubModels => new GithubModelsChatService(model),
            DBModelProvider.GoogleAI => new GoogleAI2ChatService(model),
            DBModelProvider.Ollama => new OllamaChatService(model),
            DBModelProvider.MiniMax => new MiniMaxChatService(model),
            DBModelProvider.Doubao => new DoubaoChatService(model),
            DBModelProvider.SiliconFlow => new SiliconFlowChatService(model),
            DBModelProvider.OpenRouter => new OpenRouterChatService(model, hostUrlService),
            _ => throw new NotSupportedException($"Unknown model provider: {modelProvider}")
        };
        return cs;
    }

    public ModelLoader? CreateModelLoader(DBModelProvider modelProvider)
    {
        ModelLoader? ml = modelProvider switch
        {
            DBModelProvider.Test => null,
            DBModelProvider.OpenAI => null,
            DBModelProvider.AzureOpenAI => null,
            DBModelProvider.WenXinQianFan => null,
            DBModelProvider.AliyunDashscope => null,
            DBModelProvider.ZhiPuAI => null,
            DBModelProvider.Moonshot => null,
            DBModelProvider.HunYuan => null,
            DBModelProvider.Sparkdesk => null,
            DBModelProvider.LingYi => null,
            DBModelProvider.DeepSeek => null,
            DBModelProvider.xAI => null,
            DBModelProvider.GithubModels => null,
            DBModelProvider.GoogleAI => null,
            DBModelProvider.Ollama => new OpenAIModelLoader(),
            DBModelProvider.MiniMax => null,
            DBModelProvider.Doubao => null,
            DBModelProvider.SiliconFlow => null,
            DBModelProvider.OpenRouter => new OpenAIModelLoader(),
            _ => throw new NotSupportedException($"Unknown model provider: {modelProvider}")
        };
        return ml;
    }

    public async Task<ModelValidateResult> ValidateModel(ModelKey modelKey, ModelReference modelReference, string? deploymentName, CancellationToken cancellationToken)
    {
        using ChatService cs = CreateChatService(new Model
        {
            ModelKey = modelKey, 
            ModelReference = modelReference,
            DeploymentName = deploymentName
        });
        try
        {
            ChatCompletionOptions cco = new();
            if (ModelReference.SupportReasoningEffort(modelReference.Name))
            {
                cco.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
            }

            await foreach (var seg in cs.ChatStreamedFEProcessed([new UserChatMessage("1+1=?")], cco, ChatExtraDetails.Default, cancellationToken))
            {
                if (seg.IsFromUpstream)
                {
                    return ModelValidateResult.Success();
                }
            }
            return ModelValidateResult.Success();
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "TestModel failed");
            return ModelValidateResult.Fail(e.Message);
        }
    }
}
