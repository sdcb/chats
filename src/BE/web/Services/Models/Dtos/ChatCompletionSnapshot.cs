using Chats.Web.Controllers.Api.OpenAICompatible.Dtos;
using Chats.Web.DB.Enums;

namespace Chats.Web.Services.Models.Dtos;

public sealed record ChatCompletionSnapshot
{
    public required ICollection<ChatSegment> Segments { get; init; }

    public required ChatTokenUsage Usage { get; init; }

    public required bool IsUsageReliable { get; init; }

    public required DBFinishReason FinishReason { get; init; }
}

public static class ChatCompletionSnapshotExtensions
{
    public static FullChatCompletion ToOpenAIFullChat(this ChatCompletionSnapshot snapshot, string modelName, string traceId)
    {
        return new FullChatCompletion()
        {
            Id = traceId,
            Choices =
            [
                new MessageChoice
                {
                    Index = 0,
                    FinishReason = snapshot.FinishReason.ToOpenAIFinishReason(),
                    Logprobs = null,
                    Message = snapshot.Segments.OpenAIFullResponse("assistant", null),
                }
            ],
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            SystemFingerprint = null,
            Usage = snapshot.Usage.ToOpenAIUsage(),
        };
    }

    public static ChatCompletionChunk ToFinalChunk(this ChatCompletionSnapshot snapshot, string modelName, string traceId)
    {
        return new ChatCompletionChunk
        {
            Id = traceId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelName,
            Choices =
            [
                new DeltaChoice
                {
                    Index = 0,
                    Delta = new OpenAIDelta(),
                    FinishReason = snapshot.FinishReason.ToOpenAIFinishReason(),
                    Logprobs = null
                }
            ],
            Usage = snapshot.Usage.ToOpenAIUsage(),
            SystemFingerprint = null
        };
    }

    public static Usage ToOpenAIUsage(this ChatTokenUsage usage)
    {
        PromptTokensDetails? promptDetails = usage.CacheTokens > 0
            ? new PromptTokensDetails
            {
                CachedTokens = usage.CacheTokens,
                AudioTokens = 0,
            }
            : null;

        return new Usage
        {
            CompletionTokens = usage.OutputTokens,
            PromptTokens = usage.InputTokens,
            TotalTokens = usage.InputTokens + usage.OutputTokens,
            PromptTokensDetails = promptDetails,
            CompletionTokensDetails = new CompletionTokensDetails()
            {
                ReasoningTokens = usage.ReasoningTokens
            }
        };
    }
}
