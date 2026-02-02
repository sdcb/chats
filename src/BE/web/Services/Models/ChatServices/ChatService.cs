using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Microsoft.ML.Tokenizers;
using System.Runtime.CompilerServices;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService
{
    internal static Tokenizer Tokenizer { get; } = TiktokenTokenizer.CreateForEncoding("o200k_base");

    internal static TimeSpan NetworkTimeout { get; } = TimeSpan.FromHours(24);

    public abstract IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, CancellationToken cancellationToken);

    public virtual Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken) => Task.FromResult(Array.Empty<string>());

    public virtual Task<int> CountTokenAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.EstimatePromptTokens(Tokenizer));
    }

    public async IAsyncEnumerable<ChatSegment> ChatEntry(ChatRequest request, FileUrlProvider fup, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatRequest finalRequest = await PreProcess(request, fup, cancellationToken);
        IAsyncEnumerable<ChatSegment> stream = ChatStreamed(finalRequest, cancellationToken);

        if (request.ChatConfig.Model.ThinkTagParserEnabled)
        {
            stream = ApplyThinkTagParser(stream, cancellationToken);
        }

        await foreach (ChatSegment seg in stream.WithCancellation(cancellationToken))
        {
            yield return seg;
        }
    }

    protected virtual async IAsyncEnumerable<ChatSegment> ApplyThinkTagParser(
        IAsyncEnumerable<ChatSegment> segments,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ChatSegment segment in segments.WithCancellation(cancellationToken))
        {
            yield return segment;
        }
    }

    protected virtual HashSet<string> SupportedContentTypes =>
    [
        "*"
    ];

    protected virtual async Task<ChatRequest> PreProcess(ChatRequest request, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        ChatRequest final = request;

        // Apply system prompt template replacements
        if (request.Source == UsageSource.WebChat)
        {
            string? effectiveSystemPrompt = final.GetEffectiveSystemPrompt();
            if (effectiveSystemPrompt != null)
            {
                string processedPrompt = effectiveSystemPrompt
                    .Replace("{{MODEL_NAME}}", request.ChatConfig.Model.Name)
                    .Replace("{{CURRENT_DATE}}", DateTime.UtcNow.ToString("yyyy/MM/dd"))
                    .Replace("{{CURRENT_TIME}}", DateTime.UtcNow.ToString("HH:mm:ss"));

                // If we have a System property, update it; otherwise update ChatConfig.SystemPrompt
                if (final.System != null)
                {
                    // For now, just update the first content block with the processed prompt
                    // A more sophisticated approach could preserve cache control settings
                    final = final with
                    {
                        System = NeutralSystemMessage.FromText(processedPrompt)
                    };
                }
                else
                {
                    final = final with
                    {
                        ChatConfig = final.ChatConfig.WithSystemPrompt(processedPrompt)
                    };
                }
            }
        }

        float? temperature = final.ChatConfig.Temperature;
        byte reasoningEffortId = final.ChatConfig.ReasoningEffortId;
        if (request.Source == UsageSource.WebChat)
        {
            temperature = request.ChatConfig.Model.ClampTemperature(temperature);
            reasoningEffortId = request.ChatConfig.Model.ClampReasoningEffortId(reasoningEffortId);
            if (request.ChatConfig.Model.ApiType == DBApiType.AnthropicMessages && final.ChatConfig.ThinkingBudget != null)
            {
                // invalid_request_error
                // `temperature` may only be set to 1 when thinking is enabled.
                // Please consult our documentation at https://docs.claude.com/en/docs/build-with-claude/extended-thinking#important-considerations-when-using-extended-thinking
                temperature = null;
            }

        }

        final = final with
        {
            ChatConfig = final.ChatConfig.WithClamps(temperature, reasoningEffortId),
            Messages = await (request.Source == UsageSource.WebChat
                    ? RemoveNonCurrentTurnThinkingBlocks(final.Messages)
                    : final.Messages)
                .ToAsyncEnumerable()
                .Select(async (m, ct) => await FilterVision(request.ChatConfig.Model.SupportsVisionLink, request.ChatConfig.Model.AllowVision, m, fup, ct))
                .ToListAsync(cancellationToken)
        };

        return final;
    }

    /// <summary>
    /// WebChat 场景下，历史 turn 的 thinking 对继续对话基本无用，
    /// 还会增加 prompt 体积；部分上游（例如 Anthropic thinking 规则）
    /// 也会对 thinking 的出现位置更敏感。
    /// 因此只保留「最后一个 user 消息之后（含 tool call 循环）」的 thinking，
    /// 其它（即历史 turn）消息中的 thinking 全部移除。
    /// </summary>
    internal static IList<NeutralMessage> RemoveNonCurrentTurnThinkingBlocks(IList<NeutralMessage> messages)
    {
        if (messages.Count == 0)
        {
            return messages;
        }

        // DeepSeek (thinking mode + tool calls) requires reasoning_content to be passed back
        // during the same user turn. Therefore, only remove thinking content from messages
        // BEFORE the last user message (i.e., previous turns). Keep everything after it.
        int lastUserIndex = -1;
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == NeutralChatRole.User)
            {
                lastUserIndex = i;
                break;
            }
        }

        if (lastUserIndex <= 0)
        {
            return messages;
        }

        List<NeutralMessage>? updated = null;
        for (int i = 0; i < messages.Count; i++)
        {
            NeutralMessage msg = messages[i];

            if (i < lastUserIndex && msg.Contents.Any(c => c is NeutralThinkContent))
            {
                updated ??= [.. messages];
                updated[i] = msg with
                {
                    Contents = [.. msg.Contents.Where(c => c is not NeutralThinkContent)]
                };
            }
        }

        return updated ?? messages;
    }

    protected virtual async Task<NeutralMessage> FilterVision(bool supportsVisionLink, bool allowVision, NeutralMessage message, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        List<NeutralContent> processedContents = [];

        foreach (NeutralContent content in message.Contents)
        {
            NeutralContent? toAdd = content switch
            {
                NeutralFileContent file => allowVision switch
                {
                    true => file.File.MediaType switch
                    {
                        var x when SupportedContentTypes.Contains("*") || SupportedContentTypes.Contains(x ?? "") => supportsVisionLink switch
                        {
                            true => fup.CreateNeutralImagePart(file.File),
                            false => fup.CreateNeutralImagePartForceDownload(file.File),
                        },
                        _ => null
                    },
                    false => fup.CreateNeutralTextUrl(file.File),
                },
                NeutralFileUrlContent fileUrl => allowVision switch
                {
                    true => supportsVisionLink switch
                    {
                        true => content,
                        false => await DownloadImagePart(fileUrl.Url, cancellationToken),
                    },
                    false => NeutralTextContent.Create(fileUrl.Url),
                },
                _ => content
            };

            if (toAdd != null)
            {
                processedContents.Add(toAdd);
            }
        }

        return message with { Contents = processedContents };

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
