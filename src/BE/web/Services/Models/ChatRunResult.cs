using Chats.DB.Enums;
using Chats.BE.Services.Models.Dtos;

namespace Chats.BE.Services.Models;

public sealed record ChatRunResult
{
    public required ChatCompletionSnapshot FullResponse { get; init; }

    public required DBFinishReason FinishReason { get; init; }

    public required int ReasoningDurationMs { get; init; }

    public required TimeSpan ElapsedTime { get; init; }

    public required long UserModelUsageId { get; init; }

    public Exception? Exception { get; init; }
}
