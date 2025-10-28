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
            DBModelProvider.OpenAI => model.UseAsyncApi && model.ApiType == (byte)DBApiType.Response 
                ? new ResponseApiService(model, logger)
                : model.SupportedImageSizes.Length > 0
                    ? new ImageGenerationService(model)
                    : new ChatCompletionService(model),
            DBModelProvider.AzureOpenAI => model.UseAsyncApi && model.ApiType == (byte)DBApiType.Response
                ? new AzureResponseApiService(model, logger)
                : model.SupportedImageSizes.Length > 0
                    ? new AzureImageGenerationService(model)
                    : new AzureChatCompletionService(model),
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

    public ModelLoader CreateModelLoader(DBModelProvider modelProvider)
    {
        ModelLoader ml = new OpenAIModelLoader();
        return ml;
    }

    public async Task<ModelValidateResult> ValidateModel(Model model, CancellationToken cancellationToken)
    {
        using ChatService cs = CreateChatService(model);
        try
        {
            ChatCompletionOptions cco = new();
            
            // 如果模型支持 ReasoningEffort，使用最低级别进行测试
            if (!string.IsNullOrEmpty(model.ReasoningEffortOptions))
            {
                int[] efforts = Model.GetReasoningEffortOptionsAsInt32(model.ReasoningEffortOptions);
                if (efforts.Length > 0)
                {
                    cco.ReasoningEffortLevel = ((DBReasoningEffort)efforts.Min()).ToReasoningEffort();
                }
            }

            await foreach (Dtos.InternalChatSegment seg in cs.ChatStreamedFEProcessed([new UserChatMessage("1+1=?")], cco, ChatExtraDetails.Default, cancellationToken))
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
            logger.LogInformation(e, "ValidateModel failed");
            return ModelValidateResult.Fail(e.Message);
        }
    }
}