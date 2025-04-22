using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using Mscc.GenerativeAI;
using OpenAI.Chat;
using System.Runtime.CompilerServices;
using ChatMessage = OpenAI.Chat.ChatMessage;
using FinishReason = Mscc.GenerativeAI.FinishReason;
using Model = Chats.BE.DB.Model;

namespace Chats.BE.Services.Models.ChatServices.GoogleAI;

public class GoogleAI2ChatService : ChatService
{
    private readonly GenerativeModel _generativeModel;

    private readonly List<SafetySetting> _safetySettings = 
    [
        new SafetySetting { Category = HarmCategory.HarmCategoryHateSpeech, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategorySexuallyExplicit, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryDangerousContent, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryHarassment, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryCivicIntegrity, Threshold = HarmBlockThreshold.BlockNone },
    ];
    private DBReasoningEffort _reasoningEffort = default;

    protected override bool SupportsVisionLink => false;

    public GoogleAI2ChatService(Model model) : base(model)
    {
        _generativeModel = new()
        {
            ApiKey = model.ModelKey.Secret,
            Model = model.ApiModelId,
        };
        if (_generativeModel.Timeout != NetworkTimeout)
        {
            _generativeModel.Timeout = NetworkTimeout;
        }
    }

    public bool AllowImageGeneration => Model.ModelReference.Name == "gemini-2.0-flash-exp" ||
                                        Model.ModelReference.Name == "gemini-2.0-flash-exp-image-generation";

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        GenerationConfig gc = new()
        {
            Temperature = options.Temperature,
            ResponseModalities = AllowImageGeneration ? [ResponseModality.Text, ResponseModality.Image] : [ResponseModality.Text],
        };
        if (ModelReference.SupportReasoningEffort(Model.ModelReference.Name))
        {
            gc.ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = _reasoningEffort switch
                {
                    DBReasoningEffort.Low => 0,
                    _ => null,
                }
            };
        }

        await foreach (GenerateContentResponse response in _generativeModel.GenerateContentStream(new GenerateContentRequest()
        {
            Contents = OpenAIChatMessageToGoogleContent(messages.Where(x => x is not SystemChatMessage)),
            SystemInstruction = OpenAIChatMessageToGoogleContent(messages.Where(x => x is SystemChatMessage)) switch { [] => null, var x => x[0] },
            GenerationConfig = gc,
            SafetySettings = _safetySettings,
        }, null, cancellationToken))
        {
            if (response.Candidates != null && response.Candidates.Count > 0)
            {
                string? text = response.Candidates[0].Content?.Text;
                InlineData? image = response.Candidates[0].Content?.Parts[0].InlineData;
                FinishReason? finishReason = response.Candidates[0].FinishReason;
                Dtos.ChatTokenUsage? usage = GetUsage(response.UsageMetadata);

                List<ChatSegmentItem> items = [];
                if (text != null)
                {
                    items.Add(ChatSegmentItem.FromText(text));
                }
                if (image != null)
                {
                    items.Add(ChatSegmentItem.FromBase64Image(image.Data, image.MimeType));
                }
                yield return new ChatSegment()
                {
                    FinishReason = ToChatFinishReason(finishReason),
                    Items = items,
                    Usage = usage
                };
            }
        }
    }

    private static Dtos.ChatTokenUsage? GetUsage(UsageMetadata? usageMetadata)
    {
        if (usageMetadata == null) return null;
        Dtos.ChatTokenUsage? usage = new()
        {
            InputTokens = usageMetadata.PromptTokenCount,
            OutputTokens = usageMetadata.CandidatesTokenCount,
            ReasoningTokens = usageMetadata.ThoughtsTokenCount,
        };
        return usage;
    }

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        _reasoningEffort = reasoningEffort;
    }

    static ChatFinishReason? ToChatFinishReason(FinishReason? finishReason)
    {
        if (finishReason == null)
        {
            return null;
        }

        return finishReason switch
        {
            FinishReason.FinishReasonUnspecified => null, // Assume unspecified maps to null
            FinishReason.Stop => ChatFinishReason.Stop,
            FinishReason.MaxTokens => ChatFinishReason.Length,
            FinishReason.Safety => ChatFinishReason.ContentFilter,
            FinishReason.Recitation => ChatFinishReason.ContentFilter,
            FinishReason.Other => ChatFinishReason.ContentFilter,
            FinishReason.Blocklist => ChatFinishReason.ContentFilter,
            FinishReason.ProhibitedContent => ChatFinishReason.ContentFilter,
            FinishReason.Spii => ChatFinishReason.ContentFilter,
            FinishReason.MalformedFunctionCall => ChatFinishReason.FunctionCall,
            FinishReason.Language => ChatFinishReason.ContentFilter, // Map to closest match
            FinishReason.ImageSafety => ChatFinishReason.ContentFilter, // Map to closest match
            _ => null // Handle any unknown values
        };
    }

    static List<Content> OpenAIChatMessageToGoogleContent(IEnumerable<ChatMessage> chatMessages)
    {
        return [.. chatMessages
            .Select(msg => msg switch
            {
                SystemChatMessage s => new Content("") { Role = "system", Parts = [.. msg.Content.Select(OpenAIPartToGooglePart)] },
                UserChatMessage u => new Content("") { Role = "user", Parts = [.. msg.Content.Select(OpenAIPartToGooglePart)] },
                AssistantChatMessage a => new Content("") { Role = "model", Parts = [.. msg.Content.Select(OpenAIPartToGooglePart)] },
                _ => throw new NotSupportedException($"Unsupported message type: {msg.GetType()} in {nameof(GoogleAI2ChatService)}"),
            })];
    }

    static IPart OpenAIPartToGooglePart(ChatMessageContentPart part)
    {
        return part.Kind switch
        {
            ChatMessageContentPartKind.Text => new TextData() { Text = part.Text },
            ChatMessageContentPartKind.Image => new InlineData() { Data = Convert.ToBase64String(part.ImageBytes.ToArray()), MimeType = part.ImageBytesMediaType },
            _ => throw new NotSupportedException($"Unsupported part kind: {part.Kind}"),
        };
    }

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        _generativeModel.UseGoogleSearch = enabled;
    }
}
