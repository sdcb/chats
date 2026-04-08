using Chats.BE.Services.Models.Dtos;

namespace Chats.BE.Services.Models;

public sealed record ChatRunSegmentContext
{
    public required ChatSegment Segment { get; init; }

    public required int ReasoningDurationMs { get; init; }
}
