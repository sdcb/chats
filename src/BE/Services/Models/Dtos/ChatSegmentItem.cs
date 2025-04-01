using Chats.BE.Controllers.OpenAICompatible.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;

namespace Chats.BE.Services.Models.Dtos;

public abstract record ChatSegmentItem
{
    public abstract Task<MessageContent> ToDB(
        IFileService fileService,
        FileContentTypeService contentTypeService,
        ClientInfoManager clientInfoManager,
        CurrentUser currentUser,
        int fileServiceId,
        FileImageInfoService fileImageInfoService,
        CancellationToken cancellationToken = default);
}

public record TextChatSegment : ChatSegmentItem
{
    public required string Text { get; init; }

    public override Task<MessageContent> ToDB(IFileService fileService, FileContentTypeService contentTypeService, ClientInfoManager clientInfoManager, CurrentUser currentUser, int fileServiceId, FileImageInfoService fileImageInfoService, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MessageContent.FromText(Text));
    }
}

public record ThinkChatSegment : ChatSegmentItem
{
    public required string Think { get; init; }

    public override Task<MessageContent> ToDB(IFileService fileService, FileContentTypeService contentTypeService, ClientInfoManager clientInfoManager, CurrentUser currentUser, int fileServiceId, FileImageInfoService fileImageInfoService, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MessageContent.FromThink(Think));
    }
}

public static class ChatSegmentItemExtensions
{
    public static async Task<MessageContent[]> ToDB(this ChatSegmentItem[] items, IFileService fileService, FileContentTypeService contentTypeService, ClientInfoManager clientInfoManager, CurrentUser currentUser, int fileServiceId, FileImageInfoService fileImageInfoService, CancellationToken cancellationToken = default)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToDB(fileService, contentTypeService, clientInfoManager, currentUser, fileServiceId, fileImageInfoService, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }

    public static OpenAIDelta ToOpenAIDelta(this ChatSegmentItem[] items)
    {
        return new OpenAIDelta
        {
            Content = items.OfType<TextChatSegment>().Select(x => x.Text).FirstOrDefault(),
            ReasoningContent = items.OfType<ThinkChatSegment>().Select(x => x.Think).FirstOrDefault(),
            Image = items.OfType<ChatRespImage>().FirstOrDefault(),
        };
    }

    public static OpenAIFullResponse OpenAIFullResponse(this ChatSegmentItem[] items, string role, object? refusal)
    {
        return new OpenAIFullResponse
        {
            Role = role,
            Content = items.OfType<TextChatSegment>().Select(x => x.Text).FirstOrDefault(),
            ReasoningContent = items.OfType<ThinkChatSegment>().Select(x => x.Think).FirstOrDefault(),
            Images = items.OfType<ChatRespImage>().ToArray() switch { { Length: 0 } => null, var x => x },
            Refusal = refusal,
        };
    }
}