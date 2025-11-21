using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using Microsoft.ML.Tokenizers;
using System.Runtime.CompilerServices;
using Tokenizer = Microsoft.ML.Tokenizers.Tokenizer;

namespace Chats.BE.Services.Models;

public abstract partial class ChatService(Model model) : IDisposable
{
    internal protected Model Model { get; } = model;

    internal static Tokenizer Tokenizer { get; } = TiktokenTokenizer.CreateForEncoding("o200k_base");

    protected static TimeSpan NetworkTimeout { get; } = TimeSpan.FromHours(24);

    public abstract IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, CancellationToken cancellationToken);

    public virtual async Task<ChatSegment> Chat(ChatRequest request, CancellationToken cancellationToken)
    {
        List<ChatSegmentItem> segments = [];
        ChatSegment? lastSegment = null;
        await foreach (ChatSegment seg in ChatStreamed(request, cancellationToken))
        {
            lastSegment = seg;
            segments.AddRange(seg.Items);
        }

        return new ChatSegment()
        {
            Usage = lastSegment?.Usage,
            FinishReason = lastSegment?.FinishReason,
            Items = segments,
        };
    }

    public async IAsyncEnumerable<InternalChatSegment> ChatEntry(ChatRequest request, FileUrlProvider fup, UsageSource source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatRequest newRequest = await PreProcess(request, fup, source, cancellationToken);

        if (Model.ThinkTagParserEnabled)
        {
            InternalChatSegment current = null!;
            async IAsyncEnumerable<string> TokenYielder()
            {
                await foreach (InternalChatSegment seg in ChatPrivate(newRequest, cancellationToken))
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
            await foreach (InternalChatSegment seg in ChatPrivate(newRequest, cancellationToken))
            {
                yield return seg;
            }
        }
    }

    private async IAsyncEnumerable<InternalChatSegment> ChatPrivate(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // notify inputTokenCount first to better support price calculation
        int inputTokens = request.EstimatePromptTokens(Tokenizer);
        int outputTokens = 0;
        int reasoningTokens = 0;
        yield return InternalChatSegment.InputOnly(inputTokens);

        ChatTokenUsage usageAccessor(ChatSegment seg) => new()
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens += seg.Items.GetText() switch { null => 0, var x => Tokenizer.CountTokens(x) },
            ReasoningTokens = reasoningTokens += seg.Items.GetThink() switch { null => 0, var x => Tokenizer.CountTokens(x) },
        };

        if (Model.AllowStreaming && request.Streamed)
        {
            await foreach (ChatSegment seg in ChatStreamed(request, cancellationToken))
            {
                yield return seg.ToInternal(() => usageAccessor(seg));
            }
        }
        else
        {
            ChatSegment seg = await Chat(request, cancellationToken);
            yield return seg.ToInternal(() => usageAccessor(seg));
        }
    }

    protected virtual bool SupportsVisionLink => true;
    protected virtual HashSet<string> SupportedContentTypes =>
    [
        "*"
    ];

    protected virtual async Task<ChatRequest> PreProcess(ChatRequest request, FileUrlProvider fup, UsageSource source, CancellationToken cancellationToken)
    {
        ChatRequest final = request;

        if (final.ChatConfig.SystemPrompt != null && source == UsageSource.Chat)
        {
            // Apply system prompt
            final = final with
            {
                ChatConfig = final.ChatConfig.WithSystemPrompt(final.ChatConfig.SystemPrompt
                    .Replace("{{MODEL_NAME}}", Model.Name)
                    .Replace("{{CURRENT_DATE}}", DateTime.UtcNow.ToString("yyyy/MM/dd"))
                    .Replace("{{CURRENT_TIME}}", DateTime.UtcNow.ToString("HH:mm:ss")))
            };
        }

        final = final with
        {
            ChatConfig = final.ChatConfig.WithTemperature(Model.ClampTemperature(final.ChatConfig.Temperature)),
            Steps = await final.Steps
                .ToAsyncEnumerable()
                .Select(async (m, ct) => await FilterVision(Model.AllowVision, m, fup, ct))
                .ToListAsync(cancellationToken)
        };

        return final;
    }

    protected virtual async Task<Step> FilterVision(bool allowVision, Step message, FileUrlProvider fup, CancellationToken cancellationToken)
    {
        Step final = message.Clone();

        foreach (StepContent part in message.StepContents)
        {
            StepContent? toAdd = (DBStepContentType)part.ContentTypeId switch
            {
                DBStepContentType.FileId => allowVision switch
                {
                    true => part.StepContentFile!.File.MediaType switch
                    {
                        var x when SupportedContentTypes.Contains("*") || SupportedContentTypes.Contains(x) => SupportsVisionLink switch
                        {
                            true => await fup.CreateOpenAIImagePart(part.StepContentFile!.File, cancellationToken),
                            false => await fup.CreateOpenAIImagePartForceDownload(part.StepContentFile!.File, cancellationToken),
                        },
                        _ => null
                    },
                    false => fup.CreateOpenAITextUrl(part.StepContentFile!.File),
                },
                DBStepContentType.FileUrl => allowVision switch
                {
                    true => SupportsVisionLink switch
                    {
                        true => part,
                        false => await DownloadImagePart(http, part.StepContentText!.Content, cancellationToken),
                    },
                    false => StepContent.FromText(part.StepContentText!.Content),
                },
                _ => part
            };
            if (toAdd != null)
            {
                final.StepContents.Add(toAdd);
            }
        }
        return final;

        static async Task<StepContent> DownloadImagePart(HttpClient http, string url, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp = await http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download image from {url}");
            }

            string contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return StepContent.FromFileBlob(await resp.Content.ReadAsByteArrayAsync(cancellationToken), contentType);
        }
    }

    private static readonly HttpClient http = new();

    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    protected virtual void Disposing() { }
}
