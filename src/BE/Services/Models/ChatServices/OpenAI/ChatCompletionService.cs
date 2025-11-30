using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices.OpenAI.Extensions;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class ChatCompletionService : ChatService
{
    protected override HashSet<string> SupportedContentTypes =>
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    ];

    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        OpenAIClient api = CreateOpenAIClient(modelKey, []);
        ClientResult<OpenAIModelCollection> result = await api.GetOpenAIModelClient().GetModelsAsync(cancellationToken);
        return [.. result.Value.Select(m => m.Id)];
    }

    protected virtual ChatClient CreateChatClient(Model model, params PipelinePolicy[] perCallPolicies)
    {
        OpenAIClient api = CreateOpenAIClient(model.ModelKey, perCallPolicies);
        return api.GetChatClient(model.DeploymentName);
    }

    protected virtual OpenAIClient CreateOpenAIClient(ModelKey modelKey, params PipelinePolicy[] perCallPolicies)
    {
        return OpenAIHelper.BuildOpenAIClient(modelKey, perCallPolicies);
    }

    protected virtual ReadOnlySpan<byte> ReasoningEffortPropName => "$.reasoning_content"u8;

    internal static string? TryGetDecodedValue(ref JsonPatch patch, ReadOnlySpan<byte> path)
    {
        if (patch.TryGetValue(path, out string? val))
        {
            return val;
        }

        return null;
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatClient chatClient = CreateChatClient(request.ChatConfig.Model, []);

        ChatFinishReason? finishReason = null;
        await foreach (StreamingChatCompletionUpdate delta in chatClient.CompleteChatStreamingAsync(ExtractMessages(request), ExtractOptions(request), cancellationToken))
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

    public override async Task<ChatSegment> Chat(ChatRequest request, CancellationToken cancellationToken)
    {
        ChatClient chatClient = CreateChatClient(request.ChatConfig.Model, []);
        ClientResult<ChatCompletion> cc = await chatClient.CompleteChatAsync(ExtractMessages(request), ExtractOptions(request), cancellationToken);
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

    protected virtual IEnumerable<ChatMessage> ExtractMessages(ChatRequest request)
    {
        // Use System property if available, otherwise fall back to ChatConfig.SystemPrompt
        string? systemPrompt = request.GetEffectiveSystemPrompt();
        if (systemPrompt != null)
        {
            yield return ChatMessage.CreateSystemMessage(systemPrompt);
        }

        foreach (ChatMessage msg in request.Messages.Select(ToOpenAI))
        {
            yield return msg;
        }
    }

    public ChatMessage ToOpenAI(NeutralMessage message)
    {
        return message.Role switch
        {
            NeutralChatRole.User => new UserChatMessage([.. message.Contents.Select(ToOpenAIContentPart).Where(x => x != null)]),
            NeutralChatRole.Assistant => ToAssistantMessage(message.Contents),
            NeutralChatRole.Tool => new ToolChatMessage(
                ((NeutralToolCallResponseContent)message.Contents.First()).ToolCallId,
                ((NeutralToolCallResponseContent)message.Contents.First()).Response),
            _ => throw new NotImplementedException()
        };
    }

    static ChatMessageContentPart? ToOpenAIContentPart(NeutralContent content)
    {
        return content switch
        {
            NeutralTextContent text => ChatMessageContentPart.CreateTextPart(text.Content),
            NeutralFileUrlContent fileUrl => ChatMessageContentPart.CreateImagePart(new Uri(fileUrl.Url)),
            NeutralFileBlobContent fileBlob => ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(fileBlob.Data), fileBlob.MediaType),
            NeutralErrorContent error => ChatMessageContentPart.CreateTextPart(error.Content),
            NeutralFileContent => throw new Exception("FileId content type should be converted to FileUrl or FileBlob before sending to OpenAI API in PreProcess."),
            NeutralThinkContent => null, // ChatCompletion API does not support "think" content type
            NeutralToolCallContent => null, // Tool calls are handled separately in ToAssistantMessage
            NeutralToolCallResponseContent => null, // Tool responses are handled separately
            _ => throw new NotImplementedException($"Content type {content.GetType().Name} is not supported.")
        };
    }

    static AssistantChatMessage ToAssistantMessage(IList<NeutralContent> contents)
    {
        bool hasContent = false, hasToolCall = false;
        foreach (NeutralContent content in contents)
        {
            if (content is NeutralTextContent or NeutralErrorContent or NeutralFileUrlContent or NeutralFileBlobContent)
            {
                hasContent = true;
            }
            if (content is NeutralToolCallContent)
            {
                hasToolCall = true;
            }
            if (hasContent && hasToolCall)
            {
                break; // No need to check further if both are found
            }
        }

        if (hasContent)
        {
            AssistantChatMessage msg = new(contents
                .Where(x => x is NeutralTextContent or NeutralErrorContent or NeutralFileUrlContent or NeutralFileBlobContent)
                .Select(ToOpenAIContentPart)
                .Where(x => x != null)
                .ToArray());
            foreach (ChatToolCall toolCall in contents
                .OfType<NeutralToolCallContent>()
                .Select(tc => ChatToolCall.CreateFunctionToolCall(
                    tc.Id,
                    tc.Name,
                    BinaryData.FromString(tc.Parameters))))
            {
                msg.ToolCalls.Add(toolCall);
            }
            return msg;
        }
        else if (hasToolCall)
        {
            return new(contents
                .OfType<NeutralToolCallContent>()
                .Select(tc => ChatToolCall.CreateFunctionToolCall(
                    tc.Id,
                    tc.Name,
                    BinaryData.FromString(tc.Parameters))));
        }
        else
        {
            throw new Exception("Assistant chat message must either have at least one content or tool call");
        }
    }

    protected virtual ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = new()
        {
            Temperature = request.ChatConfig.Temperature,
            EndUserId = request.EndUserId,
            AllowParallelToolCalls = request.AllowParallelToolCalls,
            ResponseFormat = request.TextFormat,
        };
        if (request.ChatConfig.MaxOutputTokens.HasValue)
        {
            cco.SetMaxTokens(request.ChatConfig.MaxOutputTokens.Value, request.ChatConfig.Model.UseMaxCompletionTokens);
        }
        foreach (ChatTool tool in request.Tools)
        {
            cco.Tools.Add(tool);
        }
        cco.TopP = request.TopP;
        cco.Seed = request.Seed;
        return cco;
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
