using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.ProviderFiles;
using Microsoft.ML.Tokenizers;
using System.Runtime.CompilerServices;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService
{
    internal static Tokenizer Tokenizer { get; } = TiktokenTokenizer.CreateForEncoding("o200k_base");

    internal static TimeSpan NetworkTimeout { get; } = TimeSpan.FromHours(24);

    public abstract IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, CancellationToken cancellationToken);

    public virtual Task<string[]> ListModels(ModelKeySnapshot modelKey, CancellationToken cancellationToken) => Task.FromResult(Array.Empty<string>());

    public virtual Task<int> CountTokenAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.EstimatePromptTokens(Tokenizer));
    }

    public async IAsyncEnumerable<ChatSegment> ChatEntry(ChatRequest request, FileUrlProvider fup, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Model model = request.GetRequiredModel();
        ProviderFileRequestContext? providerFiles = fup?.ProviderFiles?.CreateRequestContext(model.CurrentSnapshot, SupportsProviderFileReferences);

        for (int attempt = 0; ; attempt++)
        {
            providerFiles?.BeginAttempt();
            ChatRequest finalRequest = await PreProcess(request, fup!, providerFiles, cancellationToken);
            IAsyncEnumerable<ChatSegment> stream = ChatStreamed(finalRequest, cancellationToken);

            if (model.CurrentSnapshot.ThinkTagParserEnabled)
            {
                stream = ApplyThinkTagParser(stream, cancellationToken);
            }

            if (providerFiles == null || providerFiles.UsedReferences.Count == 0)
            {
                await foreach (ChatSegment seg in stream.WithCancellation(cancellationToken))
                {
                    yield return seg;
                }
                yield break;
            }

            // Provider file ids were used: if the upstream rejects them before anything was streamed,
            // invalidate the mappings and retry (re-upload once, then inline fallback).
            bool yieldedAny = false;
            ProviderFileRetryAction retry = ProviderFileRetryAction.None;
            Exception? failure = null;
            await using (IAsyncEnumerator<ChatSegment> enumerator = stream.GetAsyncEnumerator(cancellationToken))
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync();
                    }
                    catch (Exception ex) when ((retry = providerFiles.Decide(ex, yieldedAny, attempt, cancellationToken.IsCancellationRequested)) != ProviderFileRetryAction.None)
                    {
                        failure = ex;
                        break;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    yieldedAny = true;
                    yield return enumerator.Current;
                }
            }

            if (failure == null)
            {
                yield break;
            }

            await providerFiles.ApplyRetryAsync(retry, failure, cancellationToken);
        }
    }

    /// <summary>
    /// True when the service can render <see cref="NeutralProviderFileContent"/> (provider Files API file ids).
    /// </summary>
    protected virtual bool SupportsProviderFileReferences => false;

    protected static bool ContainsProviderFileReferences(ChatRequest request)
        => request.Messages.Any(m => m.Contents.Any(c => c is NeutralProviderFileContent));

    protected virtual async IAsyncEnumerable<ChatSegment> ApplyThinkTagParser(
        IAsyncEnumerable<ChatSegment> segments,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ChatSegment segment in ThinkTagParser.Parse(segments, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return segment;
        }
    }

    protected virtual HashSet<string> SupportedContentTypes =>
    [
        "*"
    ];

    protected virtual Task<ChatRequest> PreProcess(ChatRequest request, FileUrlProvider fup, CancellationToken cancellationToken)
        => PreProcess(request, fup, providerFiles: null, cancellationToken);

    protected virtual async Task<ChatRequest> PreProcess(ChatRequest request, FileUrlProvider fup, ProviderFileRequestContext? providerFiles, CancellationToken cancellationToken)
    {
        ChatRequest final = request;
        Model model = request.GetRequiredModel();

        float? temperature = final.ChatConfig.Temperature;
        string? effort = final.ChatConfig.Effort;
        if (request.Source == UsageSource.WebChat)
        {
            temperature = model.ClampTemperature(temperature);
            effort = model.ClampEffort(effort);
            if ((DBApiType)model.CurrentSnapshot.ApiTypeId == DBApiType.AnthropicMessages && final.ChatConfig.ThinkingBudget != null)
            {
                // invalid_request_error
                // `temperature` may only be set to 1 when thinking is enabled.
                // Please consult our documentation at https://docs.claude.com/en/docs/build-with-claude/extended-thinking#important-considerations-when-using-extended-thinking
                temperature = null;
            }

        }

        final = final with
        {
            ChatConfig = final.ChatConfig.WithClamps(temperature, effort),
            Messages = providerFiles is { RemoteEnabled: true }
                ? await RewriteVisionMessagesCore(
                    model.CurrentSnapshot.SupportsVisionLink,
                    model.CurrentSnapshot.AllowVision,
                    final.Messages,
                    fup,
                    providerFiles,
                    cancellationToken)
                : await RewriteVisionMessages(
                    model.CurrentSnapshot.SupportsVisionLink,
                    model.CurrentSnapshot.AllowVision,
                    final.Messages,
                    fup,
                    cancellationToken)
        };

        return final;
    }

    protected virtual Task<IList<NeutralMessage>> RewriteVisionMessages(bool supportsVisionLink, bool allowVision, IList<NeutralMessage> messages, FileUrlProvider fup, CancellationToken cancellationToken)
        => RewriteVisionMessagesCore(supportsVisionLink, allowVision, messages, fup, providerFiles: null, cancellationToken);

    protected async Task<IList<NeutralMessage>> RewriteVisionMessagesCore(bool supportsVisionLink, bool allowVision, IList<NeutralMessage> messages, FileUrlProvider fup, ProviderFileRequestContext? providerFiles, CancellationToken cancellationToken)
    {
        List<NeutralMessage> filteredMessages = new(messages.Count);

        for (int i = 0; i < messages.Count; i++)
        {
            NeutralMessage message = messages[i];
            if (message.Role == NeutralChatRole.Tool)
            {
                filteredMessages.Add(message with
                {
                    Contents = await FilterToolMessageContents(messages, i, message, supportsVisionLink, allowVision, fup, cancellationToken)
                });
                continue;
            }

            List<NeutralContent> processedContents = [];

            foreach (NeutralContent content in message.Contents)
            {
                NeutralContent? toAdd = await FilterSingleContent(
                    content,
                    supportsVisionLink,
                    allowVision && message.Role == NeutralChatRole.User,
                    fup,
                    cancellationToken);

                if (toAdd != null)
                {
                    processedContents.Add(toAdd);
                }
            }

            filteredMessages.Add(message with { Contents = processedContents });
        }

        return filteredMessages;

        async Task<List<NeutralContent>> FilterToolMessageContents(
            IList<NeutralMessage> allMessages,
            int toolMessageIndex,
            NeutralMessage toolMessage,
            bool supportsVisionLink,
            bool allowVision,
            FileUrlProvider fileUrlProvider,
            CancellationToken ct)
        {
            List<NeutralContent> contents = [];
            IReadOnlyList<NeutralToolResponseGroup> groups = toolMessage.GetToolResponseGroups();

            if (groups.Count == 0)
            {
                foreach (NeutralContent toolContent in toolMessage.Contents)
                {
                    NeutralContent? filtered = await FilterSingleContent(toolContent, supportsVisionLink, allowVision: false, fileUrlProvider, ct);
                    if (filtered != null)
                    {
                        contents.Add(filtered);
                    }
                }

                return contents;
            }

            foreach (NeutralToolResponseGroup group in groups)
            {
                contents.Add(group.ToolResponse);

                bool allowGroupImages = allowVision
                    && IsViewImageResponse(allMessages, toolMessageIndex, group.ToolResponse.ToolCallId);

                foreach (NeutralContent attachedContent in group.AttachedContents)
                {
                    NeutralContent? filtered = await FilterSingleContent(attachedContent, supportsVisionLink, allowGroupImages, fileUrlProvider, ct);
                    if (filtered != null)
                    {
                        contents.Add(filtered);
                    }
                }
            }

            return contents;
        }

        async Task<NeutralContent?> FilterSingleContent(
            NeutralContent content,
            bool supportsVisionLink,
            bool allowVision,
            FileUrlProvider fileUrlProvider,
            CancellationToken ct)
        {
            if (providerFiles != null
                && allowVision
                && content is NeutralFileContent providerCandidate
                && (SupportedContentTypes.Contains("*") || SupportedContentTypes.Contains(providerCandidate.File.MediaType ?? ""))
                && await providerFiles.TryCreateContentAsync(providerCandidate.File, providerCandidate.CacheControl, ct) is NeutralContent providerContent)
            {
                return providerContent;
            }

            return content switch
            {
                NeutralFileContent file when allowVision => file.File.MediaType switch
                {
                    var x when SupportedContentTypes.Contains("*") || SupportedContentTypes.Contains(x ?? "") => supportsVisionLink
                        ? fileUrlProvider.CreateNeutralImagePart(file.File)
                        : fileUrlProvider.CreateNeutralImagePartForceDownload(file.File),
                    _ => null
                },
                NeutralFileContent => null,
                NeutralFileUrlContent fileUrl when allowVision => supportsVisionLink
                    ? content
                    : await DownloadImagePart(fileUrl.Url, ct),
                NeutralFileUrlContent => null,
                _ => content
            };
        }

        static bool IsViewImageResponse(IList<NeutralMessage> allMessages, int toolMessageIndex, string toolCallId)
        {
            if (string.IsNullOrWhiteSpace(toolCallId) || toolMessageIndex <= 0)
            {
                return false;
            }

            for (int i = toolMessageIndex - 1; i >= 0; i--)
            {
                NeutralMessage previous = allMessages[i];
                if (previous.Role != NeutralChatRole.Assistant)
                {
                    continue;
                }

                NeutralToolCallContent? toolCall = previous.Contents
                    .OfType<NeutralToolCallContent>()
                    .FirstOrDefault(x => string.Equals(x.Id, toolCallId, StringComparison.Ordinal));
                if (toolCall != null)
                {
                    return string.Equals(toolCall.Name, "view_image", StringComparison.Ordinal);
                }
            }

            return false;
        }

        async Task<NeutralContent> DownloadImagePart(string url, CancellationToken cancellationToken)
        {
            try
            {
                (byte[] bytes, string contentType) = await fup.DownloadUrlBytesAsync(url, cancellationToken);
                return NeutralFileBlobContent.Create(bytes, contentType);
            }
            catch (Exception ex)
            {
                throw new CustomChatServiceException(DBFinishReason.UpstreamError, $"Failed to download image from {url}: {ex.Message}");
            }
        }
    }
}
