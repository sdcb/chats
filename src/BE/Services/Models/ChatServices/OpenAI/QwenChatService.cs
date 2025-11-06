using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class QwenChatService(Model model) : ChatCompletionService(model, new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"), CreateQwenPolicies())
{
    private static PipelinePolicy[] CreateQwenPolicies()
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
                
                // 检查是否有 created 字段
                if (jsonObj.TryGetPropertyValue("created", out JsonNode? createdNode)
                    && createdNode is JsonValue createdValue
                    && createdValue.TryGetValue(out long createdMs))
                {
                    if (createdMs > 253402300799L)
                    {
                        // Qwen 返回的时间戳是毫秒级别，转换为秒
                        long createdSec = createdMs / 1000;
                        jsonObj["created"] = createdSec;
                        return JSON.SerializeToUtf8Bytes(jsonObj);
                    }
                }
            }
            catch
            {
                // 如果解析失败，返回原始字节
            }
            return bytes.ToArray();
        })];
    }
    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        options.Patch.Set("$.enable_search"u8, enabled);
    }

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        if (reasoningEffort.IsLowOrMinimal())
        {
            options.Patch.Set("$.enable_thinking"u8, false);
        }
    }
}
