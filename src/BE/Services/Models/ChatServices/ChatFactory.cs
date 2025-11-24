using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using Chats.BE.Services.Models.ChatServices.GoogleAI;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.ChatServices.OpenAI.QianFan;
using Chats.BE.Services.Models.ChatServices.OpenAI.Special;
using Chats.BE.Services.Models.ChatServices.Test;
using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices;

public class ChatFactory(ILogger<ChatFactory> logger, IServiceProvider sp)
{
    public ChatService CreateChatService(Model model)
    {
        DBModelProvider modelProvider = (DBModelProvider)model.ModelKey.ModelProviderId;
        if (modelProvider == DBModelProvider.Test)
        {
            // Special case for Test model provider
            return sp.GetRequiredService<Test2ChatService>();
        }

        DBApiType apiType = model.ApiType;

        // 先按 API 类型分类，再按 ModelProvider 分类
        return apiType switch
        {
            DBApiType.OpenAIChatCompletion => modelProvider switch
            {
                DBModelProvider.AzureAIFoundry => sp.GetRequiredService<AzureAIFoundryChatService>(),
                DBModelProvider.WenXinQianFan => sp.GetRequiredService<QianFanChatService>(),
                DBModelProvider.AliyunDashscope => sp.GetRequiredService<QwenChatService>(),
                DBModelProvider.ZhiPuAI => sp.GetRequiredService<GLMChatService>(),
                DBModelProvider.HunYuan => sp.GetRequiredService<HunyuanChatService>(),
                DBModelProvider.LingYi => sp.GetRequiredService<LingYiChatService>(),
                DBModelProvider.xAI => sp.GetRequiredService<XAIChatService>(),
                DBModelProvider.GithubModels => sp.GetRequiredService<GithubModelsChatService>(),
                DBModelProvider.GoogleAI => sp.GetRequiredService<GoogleAI2ChatService>(),
                DBModelProvider.SiliconFlow => sp.GetRequiredService<SiliconFlowChatService>(),
                DBModelProvider.OpenRouter => sp.GetRequiredService<OpenRouterChatService>(),
                _ => sp.GetRequiredService<ChatCompletionService>() // Fallback to OpenAI-compatible
            },

            DBApiType.OpenAIResponse => modelProvider switch
            {
                DBModelProvider.AzureAIFoundry => sp.GetRequiredService<AzureResponseApiService>(),
                _ => sp.GetRequiredService<ResponseApiService>() // Fallback to OpenAI-compatible
            },

            DBApiType.AnthropicMessages => sp.GetRequiredService<AnthropicChatService>(),

            DBApiType.OpenAIImageGeneration => modelProvider switch
            {
                DBModelProvider.AzureAIFoundry => sp.GetRequiredService<AzureImageGenerationService>(),
                _ => sp.GetRequiredService<ImageGenerationService>() // Fallback to OpenAI-compatible
            },
            
            _ => throw new NotSupportedException($"Unknown API type: {apiType}")
        };
    }


    public async Task<ModelValidateResult> ValidateModel(Model model, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        ChatService cs = CreateChatService(model);
        try
        {
            ChatCompletionOptions cco = new();
            
            // 如果模型支持 ReasoningEffort，使用最低级别进行测试
            if (!string.IsNullOrEmpty(model.ReasoningEffortOptions))
            {
                int[] efforts = Model.GetReasoningEffortOptionsAsInt32(model.ReasoningEffortOptions);
                if (efforts.Length > 0)
                {
                    cco.ReasoningEffortLevel = ((DBReasoningEffort)efforts.Min()).ToChatCompletionReasoningEffort();
                }
            }

            await foreach (Dtos.InternalChatSegment seg in cs.ChatEntry(ChatRequest.Simple("1+1=?", model), fup, UsageSource.Validate, cancellationToken))
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