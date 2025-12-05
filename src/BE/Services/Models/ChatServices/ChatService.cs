using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
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

    public async IAsyncEnumerable<ChatSegment> ChatEntry(ChatRequest request, FileUrlProvider fup, UsageSource source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatRequest finalRequest = await PreProcess(request, fup, source, cancellationToken);
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

    protected virtual async Task<ChatRequest> PreProcess(ChatRequest request, FileUrlProvider fup, UsageSource source, CancellationToken cancellationToken)
    {
        ChatRequest final = request;

        // Apply system prompt template replacements
        if (source == UsageSource.WebChat)
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
        if (source == UsageSource.WebChat)
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
            Messages = await final.Messages
                .ToAsyncEnumerable()
                .Select(async (m, ct) => await FilterVision(request.ChatConfig.Model.SupportsVisionLink, request.ChatConfig.Model.AllowVision, m, fup, ct))
                .ToListAsync(cancellationToken)
        };

        return final;
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
                        false => await DownloadImagePart(http, fileUrl.Url, cancellationToken),
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

        static async Task<NeutralContent> DownloadImagePart(HttpClient http, string url, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp = await http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download image from {url}");
            }

            string contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return NeutralFileBlobContent.Create(await resp.Content.ReadAsByteArrayAsync(cancellationToken), contentType);
        }
    }

    private static readonly HttpClient http = new();
}
