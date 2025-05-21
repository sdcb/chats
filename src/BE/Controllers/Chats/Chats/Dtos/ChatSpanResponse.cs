using Chats.BE.DB;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record ChatSpanResponse
{
    public required byte SpanId { get; init; }

    public required Message AssistantMessage { get; init; }
}
