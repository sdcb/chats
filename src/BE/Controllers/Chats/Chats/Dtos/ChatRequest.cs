using Chats.BE.Controllers.Chats.Messages.Dtos;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record ChatRequest
{
    public required int ChatId { get; init; }

    public required byte[] SpanIds { get; init; }

    public required long? MessageId { get; init; }

    public required MessageContentRequest? UserMessage { get; init; }

    public required short TimezoneOffset { get; init; }
}