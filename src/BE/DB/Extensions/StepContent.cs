using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;

namespace Chats.BE.DB;

public partial class StepContent
{
    public ChatMessageContentPart ToTempOpenAI()
    {
        return (DBMessageContentType)ContentTypeId switch
        {
            DBMessageContentType.Text => ChatMessageContentPart.CreateTextPart(StepContentText!.Content),
            DBMessageContentType.FileId => new StepContentFilePart(StepContentFile!.File),
            DBMessageContentType.Error => ChatMessageContentPart.CreateTextPart(StepContentText!.Content),
            _ => throw new NotImplementedException()
        };
    }

    public override string ToString()
    {
        return (DBMessageContentType)ContentTypeId switch
        {
            DBMessageContentType.Text => StepContentText!.Content,
            DBMessageContentType.Error => StepContentText!.Content,
            DBMessageContentType.Reasoning => StepContentText!.Content,
            //DBMessageContentType.FileId => MessageContentUtil.ReadFileId(Content).ToString(), // not supported
            DBMessageContentType.ToolCall => $"ToolCall: {StepContentToolCall!.Name}({StepContentToolCall.Parameters})",
            DBMessageContentType.ToolCallResponse => $"ToolCallResponse: {StepContentToolCallResponse!.Response}",
            _ => throw new NotSupportedException(),
        };
    }

    public static StepContent FromText(string text)
    {
        return new StepContent { StepContentText = new() { Content = text }, ContentTypeId = (byte)DBMessageContentType.Text };
    }

    public static StepContent FromThink(string text)
    {
        return new StepContent { StepContentText = new() { Content = text }, ContentTypeId = (byte)DBMessageContentType.Reasoning };
    }

    public static StepContent FromFile(File file)
    {
        return new StepContent { StepContentFile = new() { FileId = file.Id, File = file }, ContentTypeId = (byte)DBMessageContentType.FileId };
    }

    public static StepContent FromTool(string toolCallId, string name, string parameters)
    {
        return new StepContent { StepContentToolCall = new() { Name = name, ToolCallId = toolCallId, Parameters = parameters }, ContentTypeId = (byte)DBMessageContentType.ToolCall };
    }

    public static StepContent FromToolResponse(string toolCallId, string? response, int durationMs, bool isSuccess)
    {
        return new StepContent 
        { 
            StepContentToolCallResponse = new() 
            { 
                ToolCallId = toolCallId, 
                Response = response!, 
                DurationMs = durationMs, 
                IsSuccess = isSuccess 
            }, 
            ContentTypeId = (byte)DBMessageContentType.ToolCallResponse 
        };
    }

    public static StepContent FromError(string error)
    {
        return new StepContent { StepContentText = new() { Content = error }, ContentTypeId = (byte)DBMessageContentType.Error };
    }

    public static async Task<StepContent[]> FromRequest(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .SelectAwait(async item => await item.ToMessageContent(fup, cancellationToken))
            .ToArrayAsync(cancellationToken);
    }

    public static IEnumerable<StepContent> FromFullResponse(InternalChatSegment lastSegment, string? errorText, Dictionary<ImageChatSegment, TaskCompletionSource<File>> imageMcCache)
    {
        if (errorText is not null)
        {
            yield return FromError(errorText);
        }
        // lastSegment.Items is merged now
        foreach (StepContent? item in lastSegment.Items.Select(x =>
        {
            return x switch
            {
                Base64PreviewImage => null, // skip preview images
                TextChatSegment text => FromText(text.Text),
                ThinkChatSegment think => FromThink(think.Think),
                ImageChatSegment image => FromFile(imageMcCache[image].Task.GetAwaiter().GetResult()),
                ToolCallSegment tool => FromTool(tool.Id ?? tool.Index.ToString(), tool.Name!, tool.Arguments),
                ToolCallResponseSegment toolResp => FromToolResponse(toolResp.ToolCallId, toolResp.Response, toolResp.DurationMs, toolResp.IsSuccess),
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
