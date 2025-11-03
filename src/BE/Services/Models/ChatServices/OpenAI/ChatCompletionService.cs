using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using OpenAI;
using System.Runtime.CompilerServices;
using System.ClientModel;
using Chats.BE.DB;
using System.ClientModel.Primitives;
using System.Text.Json;
using Chats.BE.Services.Models.ChatServices.OpenAI.ReasoningContents;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Extensions;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class ChatCompletionService(Model model, ChatClient chatClient) : ChatService(model)
{
    public ChatCompletionService(Model model, Uri? suggestedApiUrl = null, params PipelinePolicy[] perCallPolicies) : this(model, CreateChatClient(model, suggestedApiUrl, perCallPolicies))
    {
    }

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
        };
        foreach (PipelinePolicy policy in perCallPolicies)
        {
            oaic.AddPolicy(policy, PipelinePosition.PerCall);
        }
        OpenAIClient api = new(new ApiKeyCredential(modelKey.Secret!), oaic);
        return api;
    }

    static Func<StreamingChatCompletionUpdate, string?> StreamingReasoningContentAccessor { get; } = ReasoningContentFactory.CreateStreamingReasoningContentAccessor();
    static Func<ChatCompletion, string?> ReasoningContentAccessor { get; } = ReasoningContentFactory.CreateReasoningContentAccessor();

    protected virtual string? GetReasoningContent(ChatCompletion delta) => ReasoningContentAccessor(delta);
    protected virtual string? GetReasoningContent(StreamingChatCompletionUpdate delta) => StreamingReasoningContentAccessor(delta);

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
