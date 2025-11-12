using Chats.BE.DB;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService
{
    public async IAsyncEnumerable<InternalChatSegment> ChatStreamedFEProcessed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, FileUrlProvider fup, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatMessage[] filteredMessage = await FEPreprocess(messages, options, feOptions, fup, cancellationToken);

        if (Model.ThinkTagParserEnabled)
        {
            InternalChatSegment current = null!;
            async IAsyncEnumerable<string> TokenYielder()
            {
                await foreach (InternalChatSegment seg in ChatStreamedSimulated(suggestedStreaming: true, filteredMessage, options, cancellationToken))
                {
                    current = seg;
                    string? text = seg.Items.GetText();
                    if (text != null)
                    {
                        yield return text;
                    }
                }
            }

            await foreach (ThinkAndResponseSegment seg in ThinkTagParser.Parse(TokenYielder(), cancellationToken))
            {
                yield return current with { Items = ChatSegmentItem.FromTextAndThink(seg.Response, seg.Think) };
            }
        }
        else
        {
            await foreach (InternalChatSegment seg in ChatStreamedSimulated(suggestedStreaming: true, filteredMessage, options, cancellationToken))
            {
                yield return seg;
            }
        }
    }

    public async IAsyncEnumerable<InternalChatSegment> ChatStreamedSimulated(bool suggestedStreaming, IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // notify inputTokenCount first to better support price calculation
        int inputTokens = GetPromptTokenCount(messages);
        int outputTokens = 0;
        int reasoningTokens = 0;
        yield return InternalChatSegment.InputOnly(inputTokens);

        Dtos.ChatTokenUsage usageAccessor(ChatSegment seg) => new()
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens += seg.Items.GetText() switch { null => 0, var x => Tokenizer.CountTokens(x) },
            ReasoningTokens = reasoningTokens += seg.Items.GetThink() switch { null => 0, var x => Tokenizer.CountTokens(x) },
        };

        if (suggestedStreaming && Model.AllowStreaming)
        {
            await foreach (ChatSegment seg in ChatStreamed(messages, options, cancellationToken))
            {
                yield return seg.ToInternal(() => usageAccessor(seg));
            }
        }
        else
        {
            ChatSegment seg = await Chat(messages, options, cancellationToken);
            yield return seg.ToInternal(() => usageAccessor(seg));
        }
    }

    protected virtual bool SupportsVisionLink => true;
    protected virtual HashSet<string> SupportedContentTypes =>
    [
        "*"
    ];

    protected virtual async Task<ChatMessage> FilterVision(bool allowVision, ChatMessage message, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        List<ChatMessageContentPart> previousContents = [.. message.Content];
        message.Content.Clear();

        foreach (ChatMessageContentPart part in previousContents)
        {
            ChatMessageContentPart? toAdd = part switch
            {
                StepContentFilePart scfp => allowVision switch
                {
                    true => scfp.File.FileContentType.ContentType switch
                    {
                        var x when SupportedContentTypes.Contains("*") || SupportedContentTypes.Contains(x) => SupportsVisionLink switch
                        {
                            true => await fup.CreateOpenAIImagePart(scfp.File, cancellationToken),
                            false => await fup.CreateOpenAIImagePartForceDownload(scfp.File, cancellationToken),
                        },
                        _ => null
                    },
                    false => fup.CreateOpenAITextUrl(scfp.File),
                },
                { Kind: ChatMessageContentPartKind.Image, ImageUri: not null } => allowVision switch
                {
                    true => SupportsVisionLink switch
                    {
                        true => part,
                        false => await DownloadImagePart(http, part.ImageUri, cancellationToken),
                    },
                    false => ChatMessageContentPart.CreateTextPart(part.ImageUri.ToString()),
                },
                _ => part
            };
            if (toAdd != null)
            {
                message.Content.Add(toAdd);
            }
        }
        return message;

        static async Task<ChatMessageContentPart> DownloadImagePart(HttpClient http, Uri url, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp = await http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download image from {url}");
            }

            string contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return ChatMessageContentPart.CreateImagePart(await BinaryData.FromStreamAsync(await resp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken), contentType, null);
        }
    }

    private static readonly HttpClient http = new();
}
