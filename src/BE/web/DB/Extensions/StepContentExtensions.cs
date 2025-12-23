using Chats.DB;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB.Extensions;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using DBFile = Chats.DB.File;

namespace Chats.BE.DB.Extensions;

public static class StepContentExtensions
{
    public static async Task<StepContent[]> FromRequest(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .Select(async (item, ct) => await item.ToMessageContent(fup, ct))
            .ToArrayAsync(cancellationToken);
    }

    public static IEnumerable<StepContent> FromFullResponse(ChatCompletionSnapshot snapshot, string? errorText, Dictionary<ImageChatSegment, TaskCompletionSource<DBFile>> imageMcCache)
    {
        if (errorText is not null)
        {
            yield return StepContent.FromError(errorText);
        }
        foreach (StepContent? item in snapshot.Segments.Select(x =>
        {
            return x switch
            {
                Base64PreviewImage => null, // skip preview images
                TextChatSegment text => StepContent.FromText(text.Text),
                ThinkChatSegment think => StepContent.FromThink(think.Think, think.Signature),
                ImageChatSegment image => StepContent.FromFile(imageMcCache[image].Task.GetAwaiter().GetResult()),
                ToolCallSegment tool => StepContent.FromTool(tool.Id ?? tool.Index.ToString(), tool.Name!, tool.Arguments!),
                ToolCallResponseSegment toolResp => StepContent.FromToolResponse(toolResp.ToolCallId, toolResp.Response, toolResp.DurationMs, toolResp.IsSuccess),
                _ => throw new NotSupportedException(),
            };
        }))
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}
