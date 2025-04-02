using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class MessageContent
{
    public async Task<ChatMessageContentPart> ToOpenAI(FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return (DBMessageContentType)ContentTypeId switch
        {
            DBMessageContentType.Text => ChatMessageContentPart.CreateTextPart(MessageContentText!.Content),
            DBMessageContentType.FileId => await fup.CreateOpenAIPart(MessageContentFile, cancellationToken),
            _ => throw new NotImplementedException()
        };
    }

    public override string ToString()
    {
        return (DBMessageContentType)ContentTypeId switch
        {
            DBMessageContentType.Text => MessageContentText!.Content,
            DBMessageContentType.Error => MessageContentText!.Content,
            DBMessageContentType.Reasoning => MessageContentText!.Content,
            //DBMessageContentType.FileId => MessageContentUtil.ReadFileId(Content).ToString(), // not supported
            _ => throw new NotSupportedException(),
        };
    }

    public static MessageContent FromText(string text)
    {
        return new MessageContent { MessageContentText = new() { Content = text }, ContentTypeId = (byte)DBMessageContentType.Text };
    }

    public static MessageContent FromThink(string text)
    {
        return new MessageContent { MessageContentText = new() { Content = text }, ContentTypeId = (byte)DBMessageContentType.Reasoning };
    }

    public static MessageContent FromFile(int fileId, File file)
    {
        return new MessageContent { MessageContentFile = new() { FileId = fileId, File = file }, ContentTypeId = (byte)DBMessageContentType.FileId };
    }

    public static MessageContent FromError(string error)
    {
        return new MessageContent { MessageContentText = new() { Content = error }, ContentTypeId = (byte)DBMessageContentType.Error };
    }

    public static async Task<MessageContent[]> FromRequest(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToMessageContent(fup, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }

    public static IEnumerable<MessageContent> FromFullResponse(InternalChatSegment lastSegment, string? errorText, Dictionary<ImageChatSegment, MessageContent> imageMcCache)
    {
        if (errorText is not null)
        {
            yield return FromError(errorText);
        }
        List<ChatSegmentItem> items = lastSegment.Items.Combine();
        foreach (MessageContent item in items.Select(x =>
            {
                return x switch
                {
                    TextChatSegment text => FromText(text.Text),
                    ThinkChatSegment think => FromThink(think.Think),
                    ImageChatSegment image => imageMcCache[image],
                    _ => throw new NotSupportedException(),
                };
            }))
        {
            yield return item;
        }
    }
}
