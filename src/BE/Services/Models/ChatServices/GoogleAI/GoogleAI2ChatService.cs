using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Mscc.GenerativeAI;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinishReason = Mscc.GenerativeAI.FinishReason;
using Model = Chats.BE.DB.Model;

namespace Chats.BE.Services.Models.ChatServices.GoogleAI;

public class GoogleAI2ChatService(ChatCompletionService chatCompletionService) : ChatService
{
    private readonly List<SafetySetting> _safetySettings =
    [
        new SafetySetting { Category = HarmCategory.HarmCategoryHateSpeech, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategorySexuallyExplicit, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryDangerousContent, Threshold = HarmBlockThreshold.BlockNone },
        new SafetySetting { Category = HarmCategory.HarmCategoryHarassment, Threshold = HarmBlockThreshold.BlockNone },
    ];

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
        string? effectiveSystemPrompt = request.GetEffectiveSystemPrompt();
        GenerateContentRequest gcr = new()
        {
            Contents = ConvertMessages(request.Messages),
            SystemInstruction = effectiveSystemPrompt != null ? new Content() { Role = Role.System, Parts = [new TextData() { Text = effectiveSystemPrompt }] } : null,
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
                Maximum = 1,
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
                    FinishReason = ToDBFinishReason(finishReason),
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

    static DBFinishReason? ToDBFinishReason(FinishReason? finishReason)
    {
        if (finishReason == null)
        {
            return null;
        }

        return finishReason switch
        {
            FinishReason.FinishReasonUnspecified => null, // Assume unspecified maps to null
            FinishReason.Stop => DBFinishReason.Success,
            FinishReason.MaxTokens => DBFinishReason.Length,
            FinishReason.Safety => DBFinishReason.ContentFilter,
            FinishReason.Recitation => DBFinishReason.ContentFilter,
            FinishReason.Other => DBFinishReason.ContentFilter,
            FinishReason.Blocklist => DBFinishReason.ContentFilter,
            FinishReason.ProhibitedContent => DBFinishReason.ContentFilter,
            FinishReason.Spii => DBFinishReason.ContentFilter,
            FinishReason.MalformedFunctionCall => DBFinishReason.ToolCalls,
            FinishReason.Language => DBFinishReason.ContentFilter, // Map to closest match
            FinishReason.ImageSafety => DBFinishReason.ContentFilter, // Map to closest match
            _ => null // Handle any unknown values
        };
    }

    static List<Content> ConvertMessages(IList<NeutralMessage> messages)
    {
        return [.. messages
            .Select(msg => msg.Role switch
            {
                NeutralChatRole.User => new Content("") { Role = Role.User, Parts = [.. msg.Contents.Select(NeutralContentToGooglePart).Where(x => x != null).Select(x => x!)] },
                NeutralChatRole.Assistant => new Content("") { Role = Role.Model, Parts = AssistantMessageToParts(msg) },
                NeutralChatRole.Tool => new Content("") { Role = Role.Function, Parts = [ToolCallMessageToPart(msg)] },
                _ => throw new NotSupportedException($"Unsupported message role: {msg.Role} in {nameof(GoogleAI2ChatService)}"),
            })];

        static IPart ToolCallMessageToPart(NeutralMessage message)
        {
            NeutralContent? toolCallContent = message.Contents.FirstOrDefault();
            if (toolCallContent is not NeutralToolCallResponseContent tcr)
            {
                throw new Exception($"{nameof(ToolCallMessageToPart)} expected tool call response content but none found.");
            }

            return Part.FromFunctionResponse(tcr.ToolCallId, new
            {
                result = tcr.Response
            });
        }

        static List<IPart> AssistantMessageToParts(NeutralMessage assistantMessage)
        {
            List<IPart> results = [];
            foreach (NeutralToolCallContent toolCall in assistantMessage.Contents.OfType<NeutralToolCallContent>())
            {
                results.Add(new FunctionCall()
                {
                    Id = toolCall.Id,
                    Name = toolCall.Name,
                    Args = JsonSerializer.Deserialize<JsonObject>(toolCall.Parameters),
                });
            }

            results.AddRange(assistantMessage.Contents
                .Select(NeutralContentToGooglePart)
                .Where(x => x != null).Select(x => x!));
            return results;
        }

        static IPart? NeutralContentToGooglePart(NeutralContent content)
        {
            return content switch
            {
                NeutralTextContent text => new TextData() { Text = text.Content },
                NeutralFileBlobContent blob => new InlineData() { Data = Convert.ToBase64String(blob.Data), MimeType = blob.MediaType },
                NeutralThinkContent => null, // Skip thinking parts for Google AI
                NeutralToolCallContent => null, // Tool calls are handled separately in AssistantMessageToParts
                NeutralToolCallResponseContent => null, // Tool call responses are handled separately
                NeutralErrorContent error => new TextData() { Text = error.Content },
                _ => throw new NotSupportedException($"Unsupported content type: {content.GetType().Name}"),
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

        static Schema ToGoogleAIParameters(string? parameters)
        {
            if (string.IsNullOrEmpty(parameters)) return new Schema { Type = ParameterType.Object };
            JsonObject jsonObject = JsonSerializer.Deserialize<JsonObject>(parameters)!;
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

    public override Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        return chatCompletionService.ListModels(modelKey, cancellationToken);
    }
}
