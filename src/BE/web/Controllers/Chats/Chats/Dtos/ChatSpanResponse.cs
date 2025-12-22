using Chats.Web.DB;

namespace Chats.Web.Controllers.Chats.Chats.Dtos;

public record ChatSpanResponse
{
    public required byte SpanId { get; init; }

    public required List<ChatTurn> NewMessages { get; init; }
}
