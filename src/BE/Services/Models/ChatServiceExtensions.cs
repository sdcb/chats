using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService
{
    public async IAsyncEnumerable<InternalChatSegment> ChatStreamedFEProcessed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, ChatExtraDetails feOptions, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatMessage[] filteredMessage = await FEPreprocess(messages, options, feOptions, cancellationToken);

        if (Model.ModelReference.ReasoningResponseKindId == (byte)DBReasoningResponseKind.ThinkTag)
        {
            InternalChatSegment current = null!;
            async IAsyncEnumerable<string> TokenYielder()
            {
                await foreach (InternalChatSegment seg in ChatStreamedSimulated(suggestedStreaming: true, filteredMessage, options, cancellationToken))
                {
                    current = seg;
                    if (!string.IsNullOrEmpty(seg.Segment))
                    {
                        yield return seg.Segment;
                    }
                }
            }

            await foreach (ThinkAndResponseSegment seg in ThinkTagParser.Parse(TokenYielder(), cancellationToken))
            {
                yield return current with
                {
                    Segment = seg.Response,
                    ReasoningSegment = seg.Think,
                };
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
            OutputTokens = outputTokens += Tokenizer.CountTokens(seg.Segment!),
            ReasoningTokens = reasoningTokens += Tokenizer.CountTokens(seg.ReasoningSegment!),
        };

        if (suggestedStreaming && Model.ModelReference.AllowStreaming)
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

        async Task<ChatMessage> DownloadVision(ChatMessage message, CancellationToken cancellationToken)
        {
            using HttpClient http = new();
            return message switch
            {
                UserChatMessage userChatMessage => new UserChatMessage(await userChatMessage.Content
                    .ToAsyncEnumerable()
                    .SelectAwait(async c => c switch
                    {
                        { Kind: ChatMessageContentPartKind.Image, ImageUri: not null } => await DownloadImagePart(http, c.ImageUri, cancellationToken),
                        _ => c,
                    })
                    .ToArrayAsync(cancellationToken)),
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
        }
    }
}
