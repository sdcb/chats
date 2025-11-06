using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class SiliconFlowChatService(Model model) : ChatCompletionService(model, new Uri("https://api.siliconflow.cn/v1"), CreateSiliconflowPolicies())
{
    private static PipelinePolicy[] CreateSiliconflowPolicies()
    {
        // 创建一个 Policy 来将 Qwen 的毫秒时间戳转换为 OpenAI SDK 需要的秒时间戳
        return [new ReplaceSseContentPolicy(static bytes =>
        {
            try
            {
                // 直接从字节解析为 JsonObject
                JsonObject? jsonObj = JsonNode.Parse(bytes)?.AsObject();
                if (jsonObj == null)
                {
                    return bytes.ToArray();
                }
                
                // 检查是否有 choices 字段
                if (jsonObj.TryGetPropertyValue("choices", out JsonNode? choicesNode)
                    && choicesNode is JsonArray choicesArray
                    && choicesArray.Count == 0)
                {
                    jsonObj.Remove("choices");
                    return JSON.SerializeToUtf8Bytes(jsonObj);
                }
            }
            catch
            {
                // 如果解析失败，返回原始字节
            }

            return bytes.ToArray();
        })];
    }

    protected override Task<ChatMessage[]> FEPreprocess(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, CancellationToken cancellationToken)
    {
        options.SetMaxTokens(Model.MaxResponseTokens, Model.UseMaxCompletionTokens); // https://api-docs.deepseek.com/zh-cn/quick_start/pricing default 4096 but max 8192
        return base.FEPreprocess(messages, options, feOptions, cancellationToken);
    }

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        if (reasoningEffort.IsLowOrMinimal())
        {
            options.Patch.Set("$.enable_thinking"u8, false);
        }
    }
}