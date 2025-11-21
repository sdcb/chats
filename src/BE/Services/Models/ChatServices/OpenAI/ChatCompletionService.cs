using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI.Extensions;
using Chats.BE.Services.Models.Dtos;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class ChatCompletionService(Model model, ChatClient chatClient) : ChatService(model)
{
    public ChatCompletionService(Model model, params PipelinePolicy[] perCallPolicies) : this(model, CreateChatClient(model, perCallPolicies))
    {
    }

    protected override HashSet<string> SupportedContentTypes =>
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    ];

    private static ChatClient CreateChatClient(Model model, PipelinePolicy[] perCallPolicies)
    {
        OpenAIClient api = CreateOpenAIClient(model.ModelKey, perCallPolicies);
        return api.GetChatClient(model.DeploymentName);
    }

    internal static OpenAIClient CreateOpenAIClient(ModelKey modelKey, PipelinePolicy[] perCallPolicies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        // Fallback logic: ModelKey.Host -> ModelProviderInfo.GetInitialHost
        Uri? endpoint = !string.IsNullOrWhiteSpace(modelKey.Host)
            ? new Uri(modelKey.Host)
            : (ModelProviderInfo.GetInitialHost((DB.Enums.DBModelProvider)modelKey.ModelProviderId) switch
            {
                null => null,
                var x => new Uri(x)
            });

        OpenAIClientOptions oaic = new()
        {
            Endpoint = endpoint,
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
        if (request.ChatConfig.SystemPrompt != null)
        {
            yield return ChatMessage.CreateSystemMessage(request.ChatConfig.SystemPrompt);
        }

        foreach (ChatMessage msg in request.Steps.Select(ToOpenAI))
        {
            yield return msg;
        }
    }

    public ChatMessage ToOpenAI(Step step)
    {
        return (DBChatRole)step.ChatRoleId switch
        {
            DBChatRole.User => new UserChatMessage([.. step.StepContents.Select(ToOpenAI).Where(x => x != null)]),
            DBChatRole.Assistant => ToAssistantMessage(step.StepContents),
            DBChatRole.ToolCall => new ToolChatMessage(step.StepContents.First().StepContentToolCallResponse!.ToolCallId, step.StepContents.First().StepContentToolCallResponse!.Response),
            _ => throw new NotImplementedException()
        };

        static ChatMessageContentPart? ToOpenAI(StepContent stepContent)
        {
            return (DBStepContentType)stepContent.ContentTypeId switch
            {
                DBStepContentType.Text => ChatMessageContentPart.CreateTextPart(stepContent.StepContentText!.Content),
                DBStepContentType.FileId => throw new Exception($"FileId content type should supposed to converted to FileUrl or FileBlob before sending to OpenAI API in FEPreprocess."),
                DBStepContentType.FileUrl => ChatMessageContentPart.CreateImagePart(new Uri(stepContent.StepContentText!.Content)),
                DBStepContentType.FileBlob => ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(stepContent.StepContentBlob!.Content), stepContent.StepContentBlob.MediaType),
                DBStepContentType.Error => ChatMessageContentPart.CreateTextPart(stepContent.StepContentText!.Content),
                DBStepContentType.Think => null, // ChatCompletion api does not support "think" content type
                _ => throw new NotImplementedException()
            };
        }

        static AssistantChatMessage ToAssistantMessage(ICollection<StepContent> stepContents)
        {
            bool hasContent = false, hasToolCall = false;
            foreach (StepContent stepContent in stepContents)
            {
                if (stepContent.ContentTypeId == (byte)DBStepContentType.FileId ||
                    stepContent.ContentTypeId == (byte)DBStepContentType.Text ||
                    stepContent.ContentTypeId == (byte)DBStepContentType.Error)
                {
                    hasContent = true;
                }
                if (stepContent.ContentTypeId == (byte)DBStepContentType.ToolCall && stepContent.StepContentToolCall != null)
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
                AssistantChatMessage msg = new(stepContents
                    .Where(x => (DBStepContentType)x.ContentTypeId is DBStepContentType.FileId or DBStepContentType.Text or DBStepContentType.Error)
                    .Select(ToOpenAI)
                    .Where(x => x != null)
                    .ToArray());
                foreach (ChatToolCall toolCall in stepContents.Where(x => (DBStepContentType)x.ContentTypeId is DBStepContentType.ToolCall && x.StepContentToolCall != null)
                    .Select(content => ChatToolCall.CreateFunctionToolCall(
                        content.StepContentToolCall!.ToolCallId,
                        content.StepContentToolCall!.Name,
                        BinaryData.FromString(content.StepContentToolCall.Parameters))))
                {
                    msg.ToolCalls.Add(toolCall);
                }
                return msg;
            }
            else if (hasToolCall) // onlyToolCall
            {
                return new(stepContents.Where(x => (DBStepContentType)x.ContentTypeId is DBStepContentType.ToolCall && x.StepContentToolCall != null)
                    .Select(content => ChatToolCall.CreateFunctionToolCall(
                        content.StepContentToolCall!.ToolCallId,
                        content.StepContentToolCall!.Name,
                        BinaryData.FromString(content.StepContentToolCall.Parameters))));
            }
            else
            {
                throw new Exception("Assistant chat message must either have at least one content or tool call");
            }
        }
    }

    protected virtual ChatCompletionOptions ExtractOptions(ChatRequest request)
    {
        ChatCompletionOptions cco = new()
        {
            Temperature = request.ChatConfig.Temperature,
            EndUserId = request.EndUserId,
        };
        if (request.ChatConfig.MaxOutputTokens.HasValue)
        {
            cco.SetMaxTokens(request.ChatConfig.MaxOutputTokens.Value, Model.UseMaxCompletionTokens);
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
