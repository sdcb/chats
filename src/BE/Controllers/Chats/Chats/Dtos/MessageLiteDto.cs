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
            DBChatRole.User => new UserChatMessage(await Content
                .ToAsyncEnumerable()
                .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                .ToArrayAsync(cancellationToken)),
            DBChatRole.Assistant => AddToolCalls(new AssistantChatMessage(await Content
                .Where(x => (DBMessageContentType)x.ContentTypeId is DBMessageContentType.FileId or DBMessageContentType.Text)
                .ToAsyncEnumerable()
                .SelectAwait(async c => await c.ToOpenAI(fup, cancellationToken))
                .ToArrayAsync(cancellationToken))),
            DBChatRole.ToolCall => new ToolChatMessage(Content[0].MessageContentToolCallResponse!.ToolCallId, Content[0].MessageContentToolCallResponse!.Response),
            _ => throw new NotImplementedException()
        };
    }

    private AssistantChatMessage AddToolCalls(AssistantChatMessage assistantChatMessage)
    {
        foreach (var content in Content)
        {
            if (content.ContentTypeId == (byte)DBMessageContentType.ToolCall && content.MessageContentToolCall != null)
            {
                assistantChatMessage.ToolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                    content.MessageContentToolCall.ToolCallId,
                    content.MessageContentToolCall.Name,
                    BinaryData.FromString(content.MessageContentToolCall.Parameters)));
            }
        }
        return assistantChatMessage;
    }
}
