using Chats.Web.Controllers.Chats.Messages.Dtos;
using Chats.Web.DB.Enums;
using Chats.Web.Services.FileServices;
using Chats.Web.Services.Models.ChatServices;
using Chats.Web.Services.Models.Dtos;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Chats.Web.DB;

public partial class StepContent
{
    public DBStepContentType ContentType => (DBStepContentType)ContentTypeId;

    public StepContent Clone()
    {
        return new StepContent
        {
            ContentTypeId = ContentTypeId,
            StepContentBlob = StepContentBlob?.Clone(),
            StepContentFile = StepContentFile?.Clone(),
            StepContentText = StepContentText?.Clone(),
            StepContentThink = StepContentThink?.Clone(),
            StepContentToolCall = StepContentToolCall?.Clone(),
            StepContentToolCallResponse = StepContentToolCallResponse?.Clone()
        };
    }

    // For Visual Studio debug purposes
    public override string ToString()
    {
        return (DBStepContentType)ContentTypeId switch
        {
            DBStepContentType.Text => StepContentText!.Content,
            DBStepContentType.Error => StepContentText!.Content,
            DBStepContentType.Think => StepContentThink!.Content,
            //DBMessageContentType.FileId => MessageContentUtil.ReadFileId(Content).ToString(), // not supported
            DBStepContentType.ToolCall => $"ToolCall: {StepContentToolCall!.Name}({StepContentToolCall.Parameters})",
            DBStepContentType.ToolCallResponse => $"ToolCallResponse: {StepContentToolCallResponse!.Response}",
            _ => throw new NotSupportedException(),
        };
    }

    public static StepContent FromText(string text)
    {
        return new StepContent { StepContentText = new() { Content = text }, ContentTypeId = (byte)DBStepContentType.Text };
    }

    public static StepContent FromThink(string text, string? signature = null)
    {
        return new StepContent { StepContentThink = new() { Content = text, Signature = signature }, ContentTypeId = (byte)DBStepContentType.Think };
    }

    public static StepContent FromFile(File file)
    {
        return new StepContent { StepContentFile = new() { FileId = file.Id, File = file }, ContentTypeId = (byte)DBStepContentType.FileId };
    }

    internal static StepContent FromFileUrl(string url)
    {
        return new StepContent { StepContentText = new() { Content = url }, ContentTypeId = (byte)DBStepContentType.FileUrl };
    }

    internal static StepContent FromFileBlob(byte[] blob, string contentType)
    {
        return new StepContent { StepContentBlob = new() { Content = blob, MediaType = contentType }, ContentTypeId = (byte)DBStepContentType.FileBlob };
    }

    public static StepContent FromTool(string toolCallId, string name, string parameters)
    {
        return new StepContent { StepContentToolCall = new() { Name = name, ToolCallId = toolCallId, Parameters = parameters }, ContentTypeId = (byte)DBStepContentType.ToolCall };
    }

    public static StepContent FromToolResponse(string toolCallId, string? response, int durationMs = 0, bool isSuccess = true)
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
            ContentTypeId = (byte)DBStepContentType.ToolCallResponse
        };
    }

    public static StepContent FromError(string error)
    {
        return new StepContent { StepContentText = new() { Content = error }, ContentTypeId = (byte)DBStepContentType.Error };
    }

    public static async Task<StepContent[]> FromRequest(ContentRequestItem[] items, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        return await items
            .ToAsyncEnumerable()
            .Select(async (item, ct) => await item.ToMessageContent(fup, ct))
            .ToArrayAsync(cancellationToken);
    }

    public static IEnumerable<StepContent> FromFullResponse(ChatCompletionSnapshot snapshot, string? errorText, Dictionary<ImageChatSegment, TaskCompletionSource<File>> imageMcCache)
    {
        if (errorText is not null)
        {
            yield return FromError(errorText);
        }
        foreach (StepContent? item in snapshot.Segments.Select(x =>
        {
            return x switch
            {
                Base64PreviewImage => null, // skip preview images
                TextChatSegment text => FromText(text.Text),
                ThinkChatSegment think => FromThink(think.Think, think.Signature),
                ImageChatSegment image => FromFile(imageMcCache[image].Task.GetAwaiter().GetResult()),
                ToolCallSegment tool => FromTool(tool.Id ?? tool.Index.ToString(), tool.Name!, tool.Arguments!),
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

    public bool IsFile()
    {
        return (DBStepContentType)ContentTypeId switch
        {
            DBStepContentType.FileId => StepContentFile != null && StepContentFile.File != null,
            DBStepContentType.FileUrl => true,
            DBStepContentType.FileBlob => StepContentBlob != null,
            _ => false,
        };
    }

    public bool TryGetFileUrl([NotNullWhen(true)] out string? url)
    {
        if ((DBStepContentType)ContentTypeId == DBStepContentType.FileUrl && StepContentText != null)
        {
            url = StepContentText.Content;
            return true;
        }
        url = null;
        return false;
    }

    public bool TryGetFile([NotNullWhen(true)] out File? file)
    {
        if ((DBStepContentType)ContentTypeId == DBStepContentType.FileId && StepContentFile != null)
        {
            file = StepContentFile.File;
            return true;
        }
        file = null;
        return false;
    }

    public bool TryGetFileBlob([NotNullWhen(true)] out StepContentBlob? blob)
    {
        if ((DBStepContentType)ContentTypeId == DBStepContentType.FileBlob && StepContentBlob != null)
        {
            blob = StepContentBlob;
            return true;
        }
        blob = null;
        return false;
    }

    public bool TryGetTextPart([NotNullWhen(true)] out string? text)
    {
        if ((DBStepContentType)ContentTypeId == DBStepContentType.Text && StepContentText != null)
        {
            text = StepContentText.Content;
            return true;
        }
        text = null;
        return false;
    }

    public bool TryGetThink([NotNullWhen(true)] out string? text, out string? signature)
    {
        if ((DBStepContentType)ContentTypeId == DBStepContentType.Think && StepContentThink != null)
        {
            text = StepContentThink.Content;
            signature = StepContentThink.Signature;
            return true;
        }
        text = null;
        signature = null;
        return false;
    }

    public bool TryGetError([NotNullWhen(true)] out string? error)
    {
        if ((DBStepContentType)ContentTypeId == DBStepContentType.Error && StepContentText != null)
        {
            error = StepContentText.Content;
            return true;
        }
        error = null;
        return false;
    }
}
