using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using Mscc.GenerativeAI;
using OpenAI.Chat;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinishReason = Mscc.GenerativeAI.FinishReason;
using Model = Chats.BE.DB.Model;

namespace Chats.BE.Services.Models.ChatServices.GoogleAI;

public class GoogleAI2ChatService : ChatService
{
    private readonly List<SafetySetting> _safetySettings =
    [
        new SafetySetting { Category = HarmCategory.HarmCategoryHateSpeech, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategorySexuallyExplicit, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryDangerousContent, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryHarassment, Threshold = HarmBlockThreshold.BlockNone },
    ];

    protected override bool SupportsVisionLink => false;

    public bool AllowImageGeneration(Model model) => model.DeploymentName == "gemini-2.0-flash-exp" ||
                                        model.DeploymentName == "gemini-2.0-flash-exp-image-generation" ||
                                        model.DeploymentName == "gemini-2.5-flash-image";

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool allowImageGeneration = AllowImageGeneration(request.ChatConfig.Model);
        GenerationConfig gc = new()
        {
            Temperature = request.ChatConfig.Temperature,
            ResponseModalities = allowImageGeneration ? [ResponseModality.Text, ResponseModality.Image] : [ResponseModality.Text],
        };
        if (!allowImageGeneration && !request.ChatConfig.Model.DeploymentName.Contains("2.5-pro"))
        {
            gc.EnableEnhancedCivicAnswers = true;
        }
        if (Model.GetReasoningEffortOptionsAsInt32(request.ChatConfig.Model.ReasoningEffortOptions).Length > 0)
        {
            gc.ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = request.ChatConfig.ReasoningEffort switch
            {
                var x when x.IsLowOrMinimal() => 1024,
                    _ => null,
                },
                IncludeThoughts = true,
            };
        }

        GenerativeModel client = new()
        {
            ApiKey = request.ChatConfig.Model.ModelKey.Secret,
            Model = request.ChatConfig.Model.DeploymentName,
        };
        if (client.Timeout != NetworkTimeout)
        {
            client.Timeout = NetworkTimeout;
        }

        Tool? tool = ToGoogleAIToolCallTool(request);
        if (tool == null)
        {
            if (request.ChatConfig.CodeExecutionEnabled)
            {
                tool = new Tool()
                {
                    CodeExecution = new()
                };
            }
            
            if (request.ChatConfig.WebSearchEnabled)
            {
                client.UseGoogleSearch = true;
            }
        }

        int fcIndex = 0;
        GenerateContentRequest gcr = new()
        {
            Contents = ConvertMessages(request.Steps),
            SystemInstruction = request.ChatConfig.SystemPrompt != null ? new Content() { Role = Role.System, Parts = [new TextData() { Text = request.ChatConfig.SystemPrompt }] } : null,
            GenerationConfig = gc,
            SafetySettings = _safetySettings,
            Tools = request.ChatConfig.Model.AllowToolCall && tool != null ? [tool] : null,
        };
        Stopwatch codeExecutionSw = new();
        string? codeExecutionId = null;
        await foreach (GenerateContentResponse response in client.GenerateContentStream(gcr, new RequestOptions()
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

    static List<Content> ConvertMessages(IEnumerable<Step> steps)
    {
        return [.. steps
            .Select(msg => (DBChatRole)msg.ChatRoleId switch
            {
                DBChatRole.User => new Content("") { Role = Role.User, Parts = [.. msg.StepContents.Select(x => DBPartToGooglePart(x))] },
                DBChatRole.Assistant => new Content("") { Role = Role.Model, Parts = AssistantMessageToParts(msg) },
                DBChatRole.ToolCall => new Content("") { Role = Role.Function, Parts = [ToolCallMessageToPart(msg)] },
                _ => throw new NotSupportedException($"Unsupported message type: {msg.GetType()} in {nameof(GoogleAI2ChatService)}"),
            })];

        static IPart ToolCallMessageToPart(Step message)
        {
            StepContent? toolCallContent = message.StepContents.FirstOrDefault();
            if (toolCallContent == null || (DBStepContentType)toolCallContent.ContentTypeId != DBStepContentType.ToolCallResponse || toolCallContent.StepContentToolCallResponse == null)
            {
                throw new Exception($"{nameof(ToolCallMessageToPart)} expected tool call content but none found.");
            }

            return Part.FromFunctionResponse(toolCallContent.StepContentToolCallResponse.ToolCallId, new
            {
                result = toolCallContent.StepContentToolCallResponse.Response
            });
        }

        static List<IPart> AssistantMessageToParts(Step assistantChatMessage)
        {
            List<IPart> results = [];
            foreach (StepContentToolCall? toolCall in assistantChatMessage.StepContents.Select(x => x.StepContentToolCall))
            {
                if (toolCall != null)
                {
                    results.Add(new FunctionCall()
                    {
                        Id = toolCall.ToolCallId,
                        Name = toolCall.Name,
                        Args = JsonSerializer.Deserialize<JsonObject>(toolCall.Parameters),
                    });
                }
            }

            results.AddRange(assistantChatMessage.StepContents.Select(DBPartToGooglePart));
            return results;
        }

        static IPart DBPartToGooglePart(StepContent part)
        {
            return (DBStepContentType)part.ContentTypeId switch
            {
                DBStepContentType.Text => new TextData() { Text = part.StepContentText!.Content },
                DBStepContentType.FileBlob => new InlineData() { Data = Convert.ToBase64String(part.StepContentBlob!.Content), MimeType = part.StepContentBlob!.MediaType },
                var x => throw new NotSupportedException($"Unsupported part kind: {x}"),
            };
        }
    }

    static Tool? ToGoogleAIToolCallTool(ChatRequest cco)
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
}
