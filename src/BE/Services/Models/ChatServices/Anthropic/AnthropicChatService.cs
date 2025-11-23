using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Model = Chats.BE.DB.Model;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

public class AnthropicChatService(Model model) : ChatService(model)
{
    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AnthropicClient anthropicClient = new(new ClientOptions()
        {
            BaseUrl = new Uri(Model.ModelKey.Host ?? "https://api.anthropic.com/"),
            APIKey = Model.ModelKey.Secret,
        });

        MessageCreateParams message = ConvertOptions(request);
        int toolCallIndex = -1;
        await foreach (RawMessageStreamEvent stream in anthropicClient.Messages.CreateStreaming(message, cancellationToken))
        {
            if (stream.TryPickStart(out RawMessageStartEvent? start))
            {
                yield return ChatSegment.FromUsageOnly((int)start.Message.Usage.InputTokens, (int)start.Message.Usage.OutputTokens);
            }
            else if (stream.TryPickContentBlockStart(out RawContentBlockStartEvent? contentStart))
            {
                if (contentStart.ContentBlock.Value is ToolUseBlock toolCall)
                {
                    ++toolCallIndex;
                    yield return ChatSegment.FromToolCall(new ToolCallSegment()
                    {
                        Arguments = "",
                        Index = toolCallIndex,
                        Id = toolCall.ID,
                        Name = toolCall.Name,
                    });
                }
                else if (contentStart.ContentBlock.Value is TextBlock textBlock)
                {
                    // do nothing
                }
                else
                {
                    Console.WriteLine($"Unknown content block start: {contentStart.ContentBlock.ID}");
                }
            }
            else if (stream.TryPickContentBlockDelta(out RawContentBlockDeltaEvent? contentDelta))
            {
                if (contentDelta.Delta.TryPickThinking(out ThinkingDelta? think))
                {
                    if (think.Thinking != "")
                    {
                        yield return ChatSegment.FromThinking(think.Thinking);
                    }
                }
                else if (contentDelta.Delta.TryPickText(out TextDelta? value))
                {
                    yield return ChatSegment.FromText(value.Text);
                }
                else if (contentDelta.Delta.TryPickInputJSON(out InputJSONDelta? inputJSONDelta))
                {
                    yield return ChatSegment.FromToolCall(new ToolCallSegment()
                    {
                        Arguments = inputJSONDelta.PartialJSON,
                        Index = toolCallIndex,
                    });
                }
                else if (contentDelta.Delta.TryPickCitations(out CitationsDelta? citation))
                {
                    // ignore for now
                    //new { citation.Citation.CitedText, citation.Citation.Title, Citation = citation.Properties["citation"].ToString() }.Dump();
                }
                else if (contentDelta.Delta.TryPickSignature(out SignatureDelta? signatureDelta))
                {
                    yield return ChatSegment.FromThinkingSignature(signatureDelta.Signature);
                }
                else
                {
                    Console.WriteLine($"Unknown content delta: {contentDelta.Delta}");
                }
            }
            else if (stream.TryPickContentBlockStop(out RawContentBlockStopEvent? contentStop))
            {
                // no data, just another indicator of content stop
            }
            else if (stream.TryPickDelta(out RawMessageDeltaEvent? delta))
            {
                yield return new ChatSegment()
                {
                    FinishReason = delta.Delta.StopReason != null ? delta.Delta.StopReason.Value.Value() switch
                    {
                        StopReason.EndTurn => ChatFinishReason.Stop,
                        StopReason.MaxTokens => ChatFinishReason.Length,
                        StopReason.StopSequence => ChatFinishReason.Stop,
                        StopReason.ToolUse => ChatFinishReason.ToolCalls,
                        StopReason.PauseTurn => ChatFinishReason.Stop, // map pause to stop
                        StopReason.Refusal => ChatFinishReason.ContentFilter,
                        _ => throw new InvalidOperationException($"Unknown stop reason: {delta.Delta.StopReason.Value.Value()}"),
                    } : null,
                    Items = [],
                    Usage = new Dtos.ChatTokenUsage()
                    {
                        InputTokens = (int)delta.Usage.InputTokens!,
                        OutputTokens = (int)delta.Usage.OutputTokens,
                    }
                };
            }
            else if (stream.TryPickStop(out RawMessageStopEvent? stop))
            {
                // ignore
            }
            else
            {
                Console.WriteLine($"Unknown stream event: {stream}");
            }
        }
    }

    static MessageCreateParams ConvertOptions(ChatRequest request)
    {
        // Anthropic has a very strict policy on thinking blocks - they need pass back thinking AND signature together
        // if you only passed back thinking without signature, it would be rejected:
        // invalid_request_error
        // messages.1.content.0: Invalid `signature` in `thinking` block"

        // so you would say I can just drop the thinking blocks, this is what we do for openai
        // Yes we can drop the thinking block if there're no tool_use

        // But if there're tool_use in the same message with thinking enabled, Anthropic will reject the request:
        // invalid_request_error
        // messages.1.content.0.type: Expected `thinking` or `redacted_thinking`, but found `tool_use`. When `thinking` is enabled,
        // a final `assistant` message must start with a thinking block (preceeding the lastmost set of `tool_use` and `tool_result` blocks).
        // We recommend you include thinking blocks from previous turns. To avoid this requirement, disable `thinking`.
        // Please consult our documentation at https://docs.claude.com/en/docs/build-with-claude/extended-thinking

        // allowThinkingBlocks: only when there exists at least one thinking block AND all thinking blocks have signature
        bool hasThinkingBlocks = request.Steps
            .Where(s => s.ChatRole == DBChatRole.Assistant)
            .SelectMany(s => s.StepContents)
            .Any(sc => sc.ContentType == DBStepContentType.Think);

        bool allThinkingHaveSignature = !hasThinkingBlocks || request.Steps
            .Where(s => s.ChatRole == DBChatRole.Assistant)
            .SelectMany(s => s.StepContents)
            .Where(sc => sc.ContentType == DBStepContentType.Think)
            .All(sc => sc.StepContentThink?.Signature != null);

        bool allowThinkingBlocks = hasThinkingBlocks && allThinkingHaveSignature; // must have blocks and all signed

        // hasToolCall: assistant messages contain tool calls
        bool hasToolCall = request.Steps.Any(m => m.ChatRole == DBChatRole.Assistant && m.StepContents.Any(sc => sc.ContentType == DBStepContentType.ToolCall));

        // allowThinking: disable only when there are tool calls AND we do not have valid (signed) thinking blocks.
        // This covers both: (1) no thinking blocks + tool calls, (2) unsigned thinking blocks + tool calls.
        // Other cases: enable thinking (may drop invalid/unsigned blocks when no tool calls present).
        bool allowThinking = !hasToolCall || allowThinkingBlocks;

        return new MessageCreateParams()
        {
            MaxTokens = request.ChatConfig.Model.MaxResponseTokens,
            Model = request.ChatConfig.Model.DeploymentName,
            Messages = ConvertMessages(request.Steps, allowThinkingBlocks),
            System = request.ChatConfig.SystemPrompt switch
            {
                not null => new SystemModel(request.ChatConfig.SystemPrompt),
                null => null
            },
            Temperature = request.ChatConfig.Temperature,
            TopP = request.TopP,
            Thinking = (allowThinking && request.ChatConfig.ThinkingBudget is not null) ?
                new ThinkingConfigParam(new ThinkingConfigEnabled(request.ChatConfig.ThinkingBudget.Value))
                : null,
            Tools = [.. request.Tools.Select(ConvertTool)]
        };

        static ToolUnion ConvertTool(ChatTool tool)
        {
            return new ToolUnion(new Tool()
            {
                InputSchema = ToAnthropicSchema(tool.FunctionParameters),
                Name = tool.FunctionName,
                Description = tool.FunctionDescription,
            });

            static InputSchema ToAnthropicSchema(BinaryData binaryData)
            {
                JsonObject jsonObject = binaryData.ToObjectFromJson<JsonObject>()!;
                return new InputSchema()
                {
                    Type = JsonSerializer.SerializeToElement("object"),
                    Properties1 = jsonObject["properties"]?.AsObject().ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(MakeFieldDefObject(x.Value))),
                    Required = jsonObject["required"]?.AsArray().Select(x => (string)x!).ToList(),
                };

                static object MakeFieldDefObject(JsonNode? node)
                {
                    if (node == null)
                    {
                        throw new ArgumentNullException(nameof(node), "tool call field property must be not null.");
                    }

                    string? type = node["type"]?.GetValue<string>();
                    string? description = node["description"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(type))
                    {
                        throw new InvalidOperationException("Tool parameter field must have a type.");
                    }

                    if (description == null) return new { type };
                    return new { type, description };
                }
            }
        }

        static List<MessageParam> ConvertMessages(IEnumerable<Step> messages, bool allowThinkingBlocks)
        {
            List<Step> mergedToolMessages = MergeToolMessages(messages);
            return [.. mergedToolMessages.Select(x => ToAnthropicMessage(x, allowThinkingBlocks))];

            static List<Step> MergeToolMessages(IEnumerable<Step> messages)
            {
                // openai will omit tool messages, but anthropic needs them merged into the user message
                // for example:
                // openai: [user, assistant(request tool call, probably multiple), tool(tool response 1), tool(tool response 2), assistant]
                // anthropic: [user, assistant(request tool call, probably multiple), user(tool response 1 + 2), assistant]
                List<Step> result = [];
                List<StepContent> toolBuffer = [];

                foreach (Step message in messages)
                {
                    if (message.ChatRole == DBChatRole.ToolCall)
                    {
                        toolBuffer.AddRange(message.StepContents);
                    }
                    else
                    {
                        if (toolBuffer.Count > 0)
                        {
                            result.Add(new Step()
                            {
                                ChatRoleId = (byte)DBChatRole.User,
                                StepContents = [.. toolBuffer],
                            });
                            toolBuffer.Clear();
                        }
                        result.Add(message);
                    }
                }

                if (toolBuffer.Count > 0)
                {
                    result.Add(new Step()
                    {
                        ChatRoleId = (byte)DBChatRole.User,
                        StepContents = [.. toolBuffer],
                    });
                }

                return result;
            }

            static MessageParam ToAnthropicMessage(Step message, bool allowThinkingBlocks)
            {
                Role anthropicRole = message.ChatRole switch
                {
                    DBChatRole.User => Role.User,
                    DBChatRole.Assistant => Role.Assistant,
                    DBChatRole.ToolCall => throw new InvalidOperationException("Tool messages should be merged into user messages before conversion."),
                    _ => throw new InvalidOperationException($"Unknown message type: {message.GetType().FullName}"),
                };

                return new MessageParam()
                {
                    Role = anthropicRole,
                    Content = new MessageParamContent([.. message.StepContents
                    .Select(x => ToAnthropicMessageContent(x, allowThinkingBlocks))
                    .Where(x => x != null).Select(x => x!)
                        ])
                };

                static ContentBlockParam? ToAnthropicMessageContent(StepContent part, bool allowThinkingBlocks)
                {
                    if (part.TryGetTextPart(out string? text))
                    {
                        return new TextBlockParam(text);
                    }
                    else if (part.TryGetFileUrl(out string? url))
                    {
                        return new ImageBlockParam(new URLImageSource(url));
                    }
                    else if (part.TryGetFileBlob(out StepContentBlob? blob))
                    {
                        return new ImageBlockParam(new Base64ImageSource()
                        {
                            Data = Convert.ToBase64String(blob.Content),
                            MediaType = blob.MediaType,
                        });
                    }
                    else if (part.TryGetThink(out string? thinkText, out byte[]? signature))
                    {
                        if (allowThinkingBlocks)
                        {
                            string? signatureBase64 = signature != null ? Convert.ToBase64String(signature) : null;

                            if (thinkText == null)
                            {
                                return new RedactedThinkingBlockParam(signatureBase64!);
                            }
                            else
                            {
                                return new ThinkingBlockParam() { Thinking = thinkText, Signature = signatureBase64! };
                            }
                        }
                        else
                        {
                            return null; // drop invalid/unsigned or disallowed thinking blocks
                        }
                    }
                    else if (part.TryGetError(out string? error))
                    {
                        return new TextBlockParam(error); // map error to text
                    }
                    else if (part.StepContentToolCall is not null)
                    {
                        StepContentToolCall toolCall = part.StepContentToolCall;
                        return new ToolUseBlockParam()
                        {
                            ID = toolCall.ToolCallId,
                            Name = toolCall.Name,
                            Input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolCall.Parameters)!,
                        };
                    }
                    else if (part.StepContentToolCallResponse is not null)
                    {
                        StepContentToolCallResponse toolResponse = part.StepContentToolCallResponse;
                        return new ToolResultBlockParam()
                        {
                            ToolUseID = toolResponse.ToolCallId,
                            Content = new ToolResultBlockParamContent(toolResponse.Response),
                            IsError = !toolResponse.IsSuccess,
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unsupported StepContent type for Anthropic conversion: {(DBStepContentType)part.ContentTypeId}");
                    }
                }
            }
        }
    }
}
