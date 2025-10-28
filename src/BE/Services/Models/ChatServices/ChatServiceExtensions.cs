using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService
{
    public async IAsyncEnumerable<InternalChatSegment> ChatStreamedFEProcessed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatMessage[] filteredMessage = await FEPreprocess(messages, options, feOptions, cancellationToken);

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

    protected virtual async Task<ChatMessage> FilterVision(bool allowVision, ChatMessage message, CancellationToken cancellationToken)
    {
        if (!allowVision)
        {
            return ReplaceUserMessageImageIntoLinkText(message);
        }
        else if (SupportsVisionLink)
        {
            return message;
        }
        else
        {
            return await DownloadVision(message, cancellationToken);
        }

        static ChatMessage ReplaceUserMessageImageIntoLinkText(ChatMessage message)
        {
            return message switch
            {
                UserChatMessage userChatMessage => new UserChatMessage(userChatMessage.Content.Select(c => c.Kind switch
                {
                    var x when x == ChatMessageContentPartKind.Image => ChatMessageContentPart.CreateTextPart(c.ImageUri.ToString()),
                    _ => c,
                })),
                _ => message,
            };
        }

        static async Task<ChatMessage> DownloadVision(ChatMessage message, CancellationToken cancellationToken)
        {
            return message switch
            {
                UserChatMessage or AssistantChatMessage => await HandleMessage(http, message, cancellationToken),
                _ => message,
            };

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

            static async Task<T> HandleMessage<T>(HttpClient http, T message, CancellationToken cancellationToken) where T : ChatMessage
            {
                List<ChatMessageContentPart> previousContents = [.. message.Content];
                message.Content.Clear();
                foreach (ChatMessageContentPart part in previousContents)
                {
                    message.Content.Add(part is { Kind: ChatMessageContentPartKind.Image, ImageUri: not null } ?
                        await DownloadImagePart(http, part.ImageUri, cancellationToken) :
                        part);
                }
                return message;
            }
        }
    }

    private static readonly HttpClient http = new();
}
