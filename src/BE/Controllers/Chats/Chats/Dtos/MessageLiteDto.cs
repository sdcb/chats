using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models;
using Chats.BE.Services.FileServices;
using OpenAI.Chat;

namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public record MessageLiteDtoNoContent
{
    public required long Id { get; init; }
    public required long? ParentId { get; init; }
    public required DBChatRole Role { get; init; }
    public required byte? SpanId { get; init; }

    public MessageLiteDto WithContent(MessageContent[] content)
    {
        return new MessageLiteDto
        {
            Id = Id,
            ParentId = ParentId,
            Role = Role,
            SpanId = SpanId,
            Content = content
        };
    }
}

public record MessageLiteDto
{
    public required long Id { get; init; }
    public required long? ParentId { get; init; }
    public required DBChatRole Role { get; init; }
    public required byte? SpanId { get; init; }
    public required MessageContent[] Content { get; init; }

    public static MessageLiteDto FromDB(Message message)
    {
        return new MessageLiteDto
        {
            Id = message.Id,
            ParentId = message.ParentId,
            Role = (DBChatRole)message.ChatRoleId,
            SpanId = message.SpanId,
            Content = [.. message.MessageContents]
        };
    }

    public async Task<ChatMessage> ToOpenAI(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return Role switch
        {
            DBChatRole.System => new SystemChatMessage(await Content[0].ToOpenAI(fup, cancellationToken)),
            DBChatRole.User => new UserChatMessage(await Content
                .ToAsyncEnumerable()
                .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                .ToArrayAsync(cancellationToken)),
            DBChatRole.Assistant => new AssistantChatMessage(await Content
                .Where(x => x.ContentTypeId != (byte)DBMessageContentType.Error && x.ContentTypeId != (byte)DBMessageContentType.Reasoning)
                .ToAsyncEnumerable()
                .SelectAwait(async x => await x.ToOpenAI(fup, cancellationToken))
                .ToArrayAsync(cancellationToken)),
            _ => throw new NotImplementedException()
        };
    }
}
