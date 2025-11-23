using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.Diagnostics.CodeAnalysis;

namespace Chats.BE.DB;

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

    public static StepContent FromThink(string text, byte[]? signature = null)
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

    public static IList<StepContent> FromOpenAI(ChatMessage message)
    {
        List<StepContent> result = new(message.Content.Count);

        if (message is ToolChatMessage tool)
        {
            result.Add(FromToolResponse(tool.ToolCallId, tool.Content[0].Text));
        }
        else
        {
            foreach (ChatMessageContentPart part in message.Content)
            {
                result.AddRange(part switch
                {
                    { Kind: ChatMessageContentPartKind.Image, ImageUri: not null } => [FromFileUrl(part.ImageUri.ToString())],
                    { Kind: ChatMessageContentPartKind.Image, ImageBytes.IsEmpty: false } => [FromFileBlob(part.ImageBytes.ToArray(), part.ImageBytesMediaType)],
                    { Kind: ChatMessageContentPartKind.Text } => ParseText(part),
                    _ => throw new Exception($"Kind: {part.Kind} is not supported."),
                });
            }
        }
        return result;
    }

    private static IEnumerable<StepContent> ParseText(ChatMessageContentPart part)
    {
        if (part.Patch.TryGetValue("reasoning_content"u8, out string? reasoningContent) && !string.IsNullOrEmpty(reasoningContent))
        {
            if (part.Patch.TryGetValue("signature"u8, out string? signature) && !string.IsNullOrEmpty(signature))
            {
                yield return FromThink(reasoningContent, Convert.FromBase64String(signature!));
            }
            else
            {
                yield return FromThink(reasoningContent);
            }
        }

        if (!string.IsNullOrEmpty(part.Text))
        {
            yield return FromText(part.Text);
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

    public bool TryGetThink([NotNullWhen(true)] out string? text, out byte[]? signature)
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
