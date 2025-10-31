using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using Mscc.GenerativeAI;
using OpenAI.Chat;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
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
    ];
    private DBReasoningEffort _reasoningEffort = default;

    protected override bool SupportsVisionLink => false;

    public GoogleAI2ChatService(Model model) : base(model)
    {
        _generativeModel = new()
        {
            ApiKey = model.ModelKey.Secret,
            Model = model.DeploymentName,
        };
        if (_generativeModel.Timeout != NetworkTimeout)
        {
            _generativeModel.Timeout = NetworkTimeout;
        }
    }

    public bool AllowImageGeneration => Model.DeploymentName == "gemini-2.0-flash-exp" ||
                                        Model.DeploymentName == "gemini-2.0-flash-exp-image-generation" ||
                                        Model.DeploymentName == "gemini-2.5-flash-image";

    private bool _codeExecutionEnabled = false;

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        GenerationConfig gc = new()
        {
            Temperature = options.Temperature,
            ResponseModalities = AllowImageGeneration ? [ResponseModality.Text, ResponseModality.Image] : [ResponseModality.Text],
        };
        if (!AllowImageGeneration)
        {
            gc.EnableEnhancedCivicAnswers = true;
        }
        if (Model.GetReasoningEffortOptionsAsInt32(Model.ReasoningEffortOptions).Length > 0)
        {
            gc.ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = _reasoningEffort switch
                {
                    var x when x.IsLowOrMinimal() => 1024,
                    _ => null,
                },
                IncludeThoughts = true,
            };
        }

        Tool? tool = ToGoogleAIToolCallTool(options);
        if (tool == null && _codeExecutionEnabled)
        {
            tool = new Tool()
            {
                CodeExecution = new()
            };
        }
        if (tool != null && _generativeModel.UseGoogleSearch)
        {
            _generativeModel.UseGoogleSearch = false;
        }

        int fcIndex = 0;
        GenerateContentRequest gcr = new()
        {
            Contents = OpenAIChatMessageToGoogleContent(messages.Where(x => x is not SystemChatMessage)),
            SystemInstruction = OpenAIChatMessageToGoogleContent(messages.Where(x => x is SystemChatMessage)) switch { [] => null, var x => x[0] },
            GenerationConfig = gc,
            SafetySettings = _safetySettings,
            Tools = tool == null ? null : [tool],
        };
        Stopwatch codeExecutionSw = new();
        string? codeExecutionId = null;
        await foreach (GenerateContentResponse response in _generativeModel.GenerateContentStream(gcr, new RequestOptions()
        {
            Retry = new Retry()
            {
                Maximum = 3,
                Initial = 1,
                Multiplies = 2,
            },
            Timeout = NetworkTimeout,
        }, cancellationToken))
        {
            if (response.Candidates != null && response.Candidates.Count > 0)
            {
                List<ChatSegmentItem> items = [];
                foreach (Part part in response.Candidates[0].Content?.Parts ?? [])
                {
                    if (part.ExecutableCode != null)
                    {
                        codeExecutionId = "ce-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        items.Add(ChatSegmentItem.FromToolCall(0, new FunctionCall()
                        {
                            Id = codeExecutionId,
                            Name = part.ExecutableCode.Language.ToString(),
                            Args = new
                            {
                                code = part.ExecutableCode.Code,
                            },
                        }));
                        codeExecutionSw = Stopwatch.StartNew();
                    }
                    else if (part.CodeExecutionResult != null)
                    {
                        if (codeExecutionId == null)
                        {
                            throw new InvalidOperationException("CodeExecutionResult received without prior ExecutableCode.");
                        }
                        items.Add(ChatSegmentItem.FromToolCallResponse(codeExecutionId, part.CodeExecutionResult.Output,
                            (int)codeExecutionSw.ElapsedMilliseconds,
                            isSuccess: part.CodeExecutionResult.Outcome == Outcome.OutcomeOk));
                    }
                    else if (part.Text != null)
                    {
                        if (part.Thought == true)
                        {
                            items.Add(ChatSegmentItem.FromThink(part.Text));
                        }
                        else
                        {
                            items.Add(ChatSegmentItem.FromText(part.Text));
                        }
                    }
                    if (part.InlineData != null)
                    {
                        items.Add(ChatSegmentItem.FromBase64Image(part.InlineData.Data, part.InlineData.MimeType));
                    }
                    if (part.FunctionCall != null)
                    {
                        items.Add(ChatSegmentItem.FromToolCall(fcIndex++, part.FunctionCall));
                    }
                }

                FinishReason? finishReason = response.Candidates[0].FinishReason;
                Dtos.ChatTokenUsage? usage = GetUsage(response.UsageMetadata);

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
            OutputTokens = usageMetadata.TotalTokenCount - usageMetadata.PromptTokenCount,
            ReasoningTokens = usageMetadata.ThoughtsTokenCount,
        };
        return usage;
    }

    protected override void SetCodeExecutionEnabled(ChatCompletionOptions options, bool enabled)
    {
        _codeExecutionEnabled = enabled;
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
                SystemChatMessage s => new Content("") { Role = Role.System, Parts = [.. msg.Content.Select(x => OpenAIPartToGooglePart(x))] },
                UserChatMessage u => new Content("") { Role = Role.User, Parts = [.. msg.Content.Select(x => OpenAIPartToGooglePart(x))] },
                AssistantChatMessage a => new Content("") { Role = Role.Model, Parts = AssistantMessageToParts(a) },
                ToolChatMessage t => new Content("") { Role = Role.Function, Parts = [ToolCallMessageToPart(t)] },
                _ => throw new NotSupportedException($"Unsupported message type: {msg.GetType()} in {nameof(GoogleAI2ChatService)}"),
            })];

        static IPart ToolCallMessageToPart(ToolChatMessage message)
        {
            return Part.FromFunctionResponse(message.ToolCallId, new
            {
                result = string.Join("\r\n", message.Content.Select(x => x.Text))
            });
        }

        static List<IPart> AssistantMessageToParts(AssistantChatMessage assistantChatMessage)
        {
            List<IPart> results = [];
            if (assistantChatMessage.ToolCalls != null && assistantChatMessage.ToolCalls.Count > 0)
            {
                foreach (ChatToolCall toolCall in assistantChatMessage.ToolCalls)
                {
                    results.Add(new FunctionCall()
                    {
                        Id = toolCall.Id,
                        Name = toolCall.FunctionName,
                        Args = toolCall.FunctionArguments.ToObjectFromJson<JsonObject>(),
                    });
                }
            }

            results.AddRange(assistantChatMessage.Content.Select(OpenAIPartToGooglePart));
            return results;
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
    }

    static Tool? ToGoogleAIToolCallTool(ChatCompletionOptions cco)
    {
        if (cco.Tools == null || cco.Tools.Count == 0)
        {
            return null;
        }

        return new Tool()
        {
            FunctionDeclarations = [.. cco.Tools
                .Select(tool => new FunctionDeclaration()
                {
                    Name = tool.FunctionName,
                    Description = tool.FunctionDescription,
                    Parameters = ToGoogleAIParameters(tool.FunctionParameters),
                })],
        };

        static Schema ToGoogleAIParameters(BinaryData binaryData)
        {
            JsonObject jsonObject = binaryData.ToObjectFromJson<JsonObject>()!;
            return new Schema()
            {
                Type = ParameterType.Object,
                Properties = jsonObject["properties"]?.AsObject().ToDictionary(x => x.Key, x => new Schema()
                {
                    Type = x.Value?["type"]?.ToString() switch
                    {
                        "integer" => ParameterType.Integer,
                        "null" => ParameterType.Null,
                        "string" => ParameterType.String,
                        "number" => ParameterType.Number,
                        "boolean" => ParameterType.Boolean,
                        "array" => ParameterType.Array,
                        "object" => ParameterType.Object,
                        _ => throw new NotSupportedException($"Unsupported parameter type: {x.Value?["type"]}")
                    },
                    Description = x.Value["description"]?.ToString()!,
                }),
                Required = jsonObject["required"]?.AsArray().Select(x => (string)x!).ToList(),
            };
        }
    }

    protected override void SetWebSearchEnabled(ChatCompletionOptions options, bool enabled)
    {
        _generativeModel.UseGoogleSearch = enabled;
    }
}
