using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI.Extensions;
using Chats.BE.Services.Models.Dtos;
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
