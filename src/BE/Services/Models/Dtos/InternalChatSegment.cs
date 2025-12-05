using Chats.BE.Controllers.Api.OpenAICompatible.Dtos;
using Chats.BE.Services.Models.ChatServices;

namespace Chats.BE.Services.Models.Dtos;

public record InternalChatSegment
{
    public required DBFinishReason? FinishReason { get; init; }

    public required ICollection<ChatSegmentItem> Items { get; init; }

    public required ChatTokenUsage Usage { get; init; }

    public required bool IsUsageReliable { get; init; }

    public required bool IsFromUpstream { get; init; }

    public static InternalChatSegment Empty { get; } = new InternalChatSegment
    {
        Usage = ChatTokenUsage.Zero,
        FinishReason = null,
        Items = [],
        IsUsageReliable = false,
        IsFromUpstream = false,
    };

    public static InternalChatSegment InputOnly(int inputTokens) => Empty with { Usage = ChatTokenUsage.Zero with { InputTokens = inputTokens } };

    private string? GetFinishReasonText()
    {
        return FinishReason switch
        {
            DBFinishReason.Stop => "stop",
            DBFinishReason.Length => "length",
            DBFinishReason.ToolCalls => "tool_calls",
            DBFinishReason.ContentFilter => "content_filter",
            DBFinishReason.FunctionCall => "function_call",
            null => null,
            _ => null // For error codes, return null
        };
    }

    private Usage ToOpenAIUsage()
    {
        PromptTokensDetails? promptDetails = Usage.CacheTokens > 0
            ? new PromptTokensDetails
            {
                CachedTokens = Usage.CacheTokens,
                AudioTokens = 0
            }
            : null;

        return new Usage
        {
            CompletionTokens = Usage.OutputTokens,
            PromptTokens = Usage.InputTokens,
            TotalTokens = Usage.InputTokens + Usage.OutputTokens,
            PromptTokensDetails = promptDetails,
            CompletionTokensDetails = new CompletionTokensDetails()
            {
                ReasoningTokens = Usage.ReasoningTokens
            }
        };
    }

    internal ChatCompletionChunk ToOpenAIChunk(string modelName, string traceId)
    {
        return new()
        {
            Id = traceId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            Choices =
            [
                new DeltaChoice
                {
                    Delta = Items.ToOpenAIDelta(),
                    FinishReason = GetFinishReasonText(),
                    Index = 0,
                    Logprobs = null,
                }
            ],
            SystemFingerprint = null,
            Usage = ToOpenAIUsage(),
        };
    }

    internal FullChatCompletion ToOpenAIFullChat(string modelName, string traceId)
    {
        return new FullChatCompletion()
        {
            Id = traceId,
            Choices =
            [
                new MessageChoice
                {
                    Index = 0,
                    FinishReason = GetFinishReasonText(),
                    Logprobs = null,
                    Message = Items.OpenAIFullResponse("assistant", null),
                }
            ],
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            SystemFingerprint = null,
            Usage = ToOpenAIUsage(),
        };
    }
}
