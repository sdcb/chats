using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices.GoogleAI;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;
using Chats.BE.Services.Models.ChatServices.OpenAI.Special;
using Chats.BE.Services.Models.ChatServices.Test;
using Chats.BE.Services.Models.ModelLoaders;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices;

public class ChatFactory(ILogger<ChatFactory> logger, HostUrlService hostUrlService)
{
    public ChatService CreateChatService(Model model)
    {
        DBModelProvider modelProvider = (DBModelProvider)model.ModelKey.ModelProviderId;
        if (modelProvider == DBModelProvider.Test)
        {
            // Special case for Test model provider
            return new TestChatService(model);
        }

        DBApiType apiType = (DBApiType)model.ApiType;

        // 先按 API 类型分类，再按 ModelProvider 分类
        return apiType switch
        {
            DBApiType.ImageGeneration => modelProvider switch
            {
                DBModelProvider.OpenAI => new ImageGenerationService(model),
                DBModelProvider.AzureAIFoundry => new AzureImageGenerationService(model),
                _ => new ImageGenerationService(model) // Fallback to OpenAI-compatible
            },
            
            DBApiType.Response => modelProvider switch
            {
                DBModelProvider.OpenAI => new ResponseApiService(model, logger),
                DBModelProvider.AzureAIFoundry => new AzureResponseApiService(model, logger),
                _ => new ResponseApiService(model, logger) // Fallback to OpenAI-compatible
            },
            
            DBApiType.ChatCompletion => modelProvider switch
            {
                DBModelProvider.AzureAIFoundry => new AzureAIFoundryChatService(model),
                DBModelProvider.WenXinQianFan => new QianFanChatService(model),
                DBModelProvider.AliyunDashscope => new QwenChatService(model),
                DBModelProvider.ZhiPuAI => new GLMChatService(model),
                DBModelProvider.HunYuan => new HunyuanChatService(model),
                DBModelProvider.LingYi => new LingYiChatService(model),
                DBModelProvider.xAI => new XAIChatService(model),
                DBModelProvider.GithubModels => new GithubModelsChatService(model),
                DBModelProvider.GoogleAI => new GoogleAI2ChatService(model),
                DBModelProvider.SiliconFlow => new SiliconFlowChatService(model),
                DBModelProvider.OpenRouter => new OpenRouterChatService(model, hostUrlService),
                _ => new ChatCompletionService(model) // Fallback to OpenAI-compatible
            },
            
            _ => throw new NotSupportedException($"Unknown API type: {apiType}")
        };
    }

    public ModelLoader CreateModelLoader(DBModelProvider _)
    {
        ModelLoader ml = new OpenAIModelLoader();
        return ml;
    }

    public async Task<ModelValidateResult> ValidateModel(Model model, FileUrlProvider fup, CancellationToken cancellationToken)
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

            await foreach (Dtos.InternalChatSegment seg in cs.ChatStreamedFEProcessed([new UserChatMessage("1+1=?")], cco, ChatExtraDetails.Default, fup, cancellationToken))
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