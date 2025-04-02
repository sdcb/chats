﻿using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using OpenAI;
using System.Runtime.CompilerServices;
using System.ClientModel;
using Chats.BE.DB;
using System.ClientModel.Primitives;
using System.Text.Json;
using Chats.BE.Services.Models.ChatServices.OpenAI.ReasoningContents;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class OpenAIChatService(Model model, ChatClient chatClient) : ChatService(model)
{
    public OpenAIChatService(Model model, Uri? suggestedApiUrl = null, params PipelinePolicy[] perCallPolicies) : this(model, CreateChatClient(model, suggestedApiUrl, perCallPolicies))
    {
    }

    private static ChatClient CreateChatClient(Model model, Uri? suggestedApiUrl, PipelinePolicy[] perCallPolicies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));
        OpenAIClientOptions oaic = new()
        {
            Endpoint = !string.IsNullOrWhiteSpace(model.ModelKey.Host) ? new Uri(model.ModelKey.Host) : suggestedApiUrl,
            NetworkTimeout = TimeSpan.FromHours(1),
        };
        foreach (PipelinePolicy policy in perCallPolicies)
        {
            oaic.AddPolicy(policy, PipelinePosition.PerCall);
        }
        OpenAIClient api = new(new ApiKeyCredential(model.ModelKey.Secret!), oaic);
        return api.GetChatClient(model.ApiModelId);
    }

    static Func<StreamingChatCompletionUpdate, string?> StreamingReasoningContentAccessor { get; } = ReasoningContentFactory.CreateStreamingReasoningContentAccessor();
    static Func<ChatCompletion, string?> ReasoningContentAccessor { get; } = ReasoningContentFactory.CreateReasoningContentAccessor();

    protected virtual string? GetReasoningContent(ChatCompletion delta) => ReasoningContentAccessor(delta);
    protected virtual string? GetReasoningContent(StreamingChatCompletionUpdate delta) => StreamingReasoningContentAccessor(delta);

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (StreamingChatCompletionUpdate delta in chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            string? segment = delta.ContentUpdate.FirstOrDefault()?.Text;
            string? reasoningSegment = GetReasoningContent(delta);

            if (segment == null && reasoningSegment == null && delta.Usage == null)
            {
                continue;
            }

            yield return new ChatSegment
            {
                Items = ChatSegmentItem.FromTextAndThink(segment, reasoningSegment),
                FinishReason = delta.FinishReason,
                Usage = delta.Usage != null ? new Dtos.ChatTokenUsage()
                {
                    InputTokens = delta.Usage.InputTokenCount,
                    OutputTokens = delta.Usage.OutputTokenCount,
                    ReasoningTokens = delta.Usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
                } : null,
            };
        }
    }

    public override async Task<ChatSegment> Chat(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken)
    {
        if (Model.ModelReference.IsSdkUnsupportedO1)
        {
            // must use replace system chat message into developer chat message for unsupported model
            messages = [.. messages.Select(m => m switch
            {
                SystemChatMessage sys => new DeveloperChatMessage(sys.Content[0].Text),
                _ => m
            })];
        }

        ClientResult<ChatCompletion> cc = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        ChatCompletion delta = cc.Value;
        return new ChatSegment
        {
            Items = ChatSegmentItem.FromTextAndThink(delta.Content[0].Text, GetReasoningContent(delta)),
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

    private class DeveloperChatMessage(string content) : SystemChatMessage(content), IJsonModel<DeveloperChatMessage>
    {
        public DeveloperChatMessage Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            throw new NotImplementedException();
        }

        public DeveloperChatMessage Create(BinaryData data, ModelReaderWriterOptions options)
        {
            throw new NotImplementedException();
        }

        public string GetFormatFromOptions(ModelReaderWriterOptions options)
        {
            throw new NotImplementedException();
        }

        public void Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("role"u8);
            writer.WriteStringValue("developer");
            writer.WritePropertyName("content"u8);
            writer.WriteStringValue(Content[0].Text);
            writer.WriteEndObject();
        }

        public BinaryData Write(ModelReaderWriterOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
