using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using OpenAI;
using System.Runtime.CompilerServices;
using System.ClientModel;
using Chats.BE.DB;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class ChatCompletionService(Model model, ChatClient chatClient) : ChatService(model)
{
    public ChatCompletionService(Model model, Uri? suggestedApiUrl = null, params PipelinePolicy[] perCallPolicies) : this(model, CreateChatClient(model, suggestedApiUrl, perCallPolicies))
    {
    }

    protected override HashSet<string> SupportedContentTypes =>
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    ];

    private static ChatClient CreateChatClient(Model model, Uri? suggestedApiUrl, PipelinePolicy[] perCallPolicies)
    {
        OpenAIClient api = CreateOpenAIClient(model.ModelKey, suggestedApiUrl, perCallPolicies);
        return api.GetChatClient(model.DeploymentName);
    }

    internal static OpenAIClient CreateOpenAIClient(ModelKey modelKey, Uri? suggestedApiUrl, PipelinePolicy[] perCallPolicies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));
        OpenAIClientOptions oaic = new()
        {
            Endpoint = !string.IsNullOrWhiteSpace(modelKey.Host) ? new Uri(modelKey.Host) : suggestedApiUrl,
            NetworkTimeout = NetworkTimeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0), 
        };
        foreach (PipelinePolicy policy in perCallPolicies)
        {
            oaic.AddPolicy(policy, PipelinePosition.PerCall);
        }
        OpenAIClient api = new(new ApiKeyCredential(modelKey.Secret!), oaic);
        return api;
    }

    protected virtual ReadOnlySpan<byte> ReasoningEffortPropName => "$.reasoning_content"u8;

    /// <summary>
    /// 从 JsonPatch 中安全地提取并解码字符串值
    /// Workaround for Azure SDK issues:
    /// - #53716: TryGetValue throws InvalidOperationException when path doesn't exist
    /// - #53718: TryGetValue doesn't decode JSON escape sequences (\n, \t, etc.)
    /// </summary>
    /// <param name="patch">要查询的 JsonPatch 对象</param>
    /// <param name="path">JSON 路径</param>
    /// <returns>解码后的字符串值，如果路径不存在或值为 null 则返回 null</returns>
    internal static string? TryGetDecodedValue(ref JsonPatch patch, ReadOnlySpan<byte> path)
    {
        // Workaround for #53716: Contains() 方法不会抛出异常，用于检查路径是否存在
        if (!patch.Contains(path))
        {
            return null;
        }

        // 尝试获取原始值
        if (!patch.TryGetValue(path, out string? val) || val == null)
        {
            return null;
        }

        // Workaround for #53718: TryGetValue 不会自动解码 JSON 转义序列
        // 使用 JsonSerializer.Deserialize 来正确处理 \n, \t, \", \\, \uXXXX 等转义字符
        try
        {
            return JsonSerializer.Deserialize<string>($"\"{val}\"");
        }
        catch
        {
            // 如果反序列化失败（例如值已经被正确解码，或包含无效的转义序列），
            // 则返回原始值作为后备方案
            return val;
        }
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatFinishReason? finishReason = null;
        await foreach (StreamingChatCompletionUpdate delta in chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            string? segment = delta.ContentUpdate.FirstOrDefault()?.Text;
            string? reasoningSegment = GetReasoningContent(delta);

            if (segment == null && reasoningSegment == null && delta.Usage == null && (delta.ToolCallUpdates == null || delta.ToolCallUpdates.Count == 0) && delta.FinishReason == null)
            {
                continue;
            }

            yield return new ChatSegment
            {
                Items = ChatSegmentItem.FromTextThinkToolCall(segment, reasoningSegment, delta.ToolCallUpdates),
                FinishReason = (finishReason ??= delta.FinishReason),
                Usage = delta.Usage != null ? GetUsage(delta.Usage) : null,
            };
        }

        string? GetReasoningContent(StreamingChatCompletionUpdate delta)
        {
            if (delta.Choices.Count == 0) return null;
            return TryGetDecodedValue(ref delta.Choices[0].Delta.Patch, ReasoningEffortPropName);
        }
    }

    public override async Task<ChatSegment> Chat(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken)
    {
        // SupportsDeveloperMessage 功能已移除，所有模型都不再支持 DeveloperMessage
        
        ClientResult<ChatCompletion> cc = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        ChatCompletion delta = cc.Value;
        return new ChatSegment
        {
            Items = ChatSegmentItem.FromTextThinkToolCall(delta.Content.Count > 0 ? delta.Content[0].Text : null, GetReasoningContent(delta), delta.ToolCalls),
            FinishReason = delta.FinishReason,
            Usage = delta.Usage != null ? GetUsage(delta.Usage) : null,
        };

        string? GetReasoningContent(ChatCompletion delta)
        {
            return TryGetDecodedValue(ref delta.Choices[0].Patch, ReasoningEffortPropName);
        }
    }

    protected virtual Dtos.ChatTokenUsage GetUsage(global::OpenAI.Chat.ChatTokenUsage usage)
    {
        return new Dtos.ChatTokenUsage()
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            ReasoningTokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
        };
    }
}
