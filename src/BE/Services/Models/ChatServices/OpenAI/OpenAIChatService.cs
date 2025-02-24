using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using OpenAI;
using System.Runtime.CompilerServices;
using System.ClientModel;
using Chats.BE.DB;
using System.ClientModel.Primitives;
using System.Text.Json;
using Chats.BE.Services.Models.ChatServices.OpenAI.ReasoningContents;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class OpenAIChatService : ChatService
{
    private readonly ChatClient _chatClient;

    public OpenAIChatService(Model model, Uri? suggestedApiUrl = null) : base(model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        OpenAIClient api = new(new ApiKeyCredential(model.ModelKey.Secret!), new OpenAIClientOptions()
        {
            Endpoint = !string.IsNullOrWhiteSpace(model.ModelKey.Host) ? new Uri(model.ModelKey.Host) : suggestedApiUrl,
        });
        _chatClient = api.GetChatClient(model.ApiModelId);
    }

    public OpenAIChatService(Model model, ChatClient chatClient) : base(model)
    {
        _chatClient = chatClient;
    }

    static Func<StreamingChatCompletionUpdate, string?> StreamingReasoningContentAccessor { get; } = ReasoningContentFactory.CreateStreamingReasoningContentAccessor();
    static Func<ChatCompletion, string?> ReasoningContentAccessor { get; } = ReasoningContentFactory.CreateReasoningContentAccessor();

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (StreamingChatCompletionUpdate delta in _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            string? segment = delta.ContentUpdate.FirstOrDefault()?.Text;
            string? reasoningSegment = StreamingReasoningContentAccessor(delta);

            if (segment == null && reasoningSegment == null && delta.Usage == null)
            {
                continue;
            }

            yield return new ChatSegment
            {
                Segment =  segment,
                ReasoningSegment = reasoningSegment,
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
            messages = messages.Select(m => m switch
            {
                SystemChatMessage sys => new DeveloperChatMessage(sys.Content[0].Text),
                _ => m
            }).ToList();
        }

        ClientResult<ChatCompletion> cc = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        ChatCompletion delta = cc.Value;
        return new ChatSegment
        {
            Segment = delta.Content[0].Text,
            ReasoningSegment = ReasoningContentAccessor(delta),
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
