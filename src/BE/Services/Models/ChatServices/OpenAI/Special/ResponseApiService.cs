using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ResponseApiService(Model model, ILogger logger, OpenAIResponseClient responseClient) : ChatService(model)
{
    public ResponseApiService(Model model, ILogger logger, Uri? suggestedUri = null, params PipelinePolicy[] perCallPolicies)
        : this(model, logger, CreateResponseAPI(model, suggestedUri, perCallPolicies))
    {
    }

    static OpenAIResponseClient CreateResponseAPI(Model model, Uri? suggestedUri, PipelinePolicy[] pipelinePolicies)
    {
        OpenAIClient api = ChatCompletionService.CreateOpenAIClient(model.ModelKey, suggestedUri, pipelinePolicies);
        OpenAIResponseClient cc = api.GetOpenAIResponseClient(model.DeploymentName);
        return cc;
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool hasTools = false;
        if (Model.UseAsyncApi)
        {
            Stopwatch sw = Stopwatch.StartNew();
            OpenAIResponse response = await responseClient.CreateResponseAsync(ToResponse(messages), ToResponse(options, background: true), cancellationToken);

            cancellationToken.Register(async () =>
            {
                if (response.Status == ResponseStatus.InProgress || response.Status == ResponseStatus.Queued)
                {
                    try
                    {
                        await responseClient.CancelResponseAsync(response.Id, default(CancellationToken));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error cancelling response {response.Id}: {ex.Message}", response.Id, ex.Message);
                    }
                }
            });

            bool cancelled = false;
            try
            {
                while (response.Status == ResponseStatus.InProgress || response.Status == ResponseStatus.Queued)
                {
                    logger.LogInformation("{response.Id} status: {response.Status}, elapsed: {sw.ElapsedMilliseconds:N0}ms", response.Id, response.Status, sw.ElapsedMilliseconds);
                    await Task.Delay(2000, cancellationToken);
                    response = await responseClient.GetResponseAsync(response.Id, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                cancelled = true;
            }

            logger.LogInformation("Response {response.Id} completed with status: {response.Status}, elapsed={sw.ElapsedMilliseconds:N0}ms", response.Id, response.Status, sw.ElapsedMilliseconds);

            if (response.Status == ResponseStatus.Incomplete)
            {
                yield return ChatSegment.Completed(new Dtos.ChatTokenUsage()
                {
                    InputTokens = response.Usage.InputTokenCount,
                    OutputTokens = response.Usage.OutputTokenCount,
                    ReasoningTokens = response.Usage.OutputTokenDetails.ReasoningTokenCount,
                }, ChatFinishReason.Length);
            }
            else if (response.Status == ResponseStatus.Failed)
            {
                yield return ChatSegment.Completed(new Dtos.ChatTokenUsage()
                {
                    InputTokens = response.Usage.InputTokenCount,
                    OutputTokens = response.Usage.OutputTokenCount,
                    ReasoningTokens = response.Usage.OutputTokenDetails.ReasoningTokenCount,
                }, ChatFinishReason.Length);
                throw new CustomChatServiceException(DBFinishReason.ContentFilter, response.Error.Message ?? "Response failed");
            }
            else if (response.Status == ResponseStatus.Cancelled || cancelled)
            {
                yield return new ChatSegment()
                {
                    Usage = new Dtos.ChatTokenUsage()
                    {
                        InputTokens = response.Usage.InputTokenCount,
                        OutputTokens = response.Usage.OutputTokenCount,
                        ReasoningTokens = response.Usage.OutputTokenDetails.ReasoningTokenCount,
                    },
                    FinishReason = null, Items = [],
                };
                throw new TaskCanceledException();
            }
            else if (response.Status != ResponseStatus.Completed)
            {
                throw new NotSupportedException($"Unsupported response status: {response.Status}");
            }
            else
            {
                // Completed
                int fcIndex = 0;
                foreach (ResponseItem item in response.OutputItems)
                {
                    if (item is ReasoningResponseItem thinkItem)
                    {
                        if (thinkItem.SummaryParts.Count > 0)
                        {
                            yield return ChatSegment.FromThinkOnly(string.Join(
                                separator: "\n\n",
                                thinkItem.SummaryParts.Select(part => (part as ReasoningSummaryTextPart)?.Text ?? string.Empty)));
                        }
                    }
                    else if (item is FunctionCallResponseItem fc)
                    {
                        hasTools = true;
                        yield return ChatSegment.FromToolCall(fcIndex++, fc);
                    }
                    else if (item is MessageResponseItem msg)
                    {
                        foreach (ResponseContentPart part in msg.Content)
                        {
                            if (part.Kind == ResponseContentPartKind.OutputText)
                            {
                                yield return ChatSegment.FromTextOnly(part.Text);
                            }
                            else if (part.Kind == ResponseContentPartKind.Refusal)
                            {
                                throw new CustomChatServiceException(DBFinishReason.ContentFilter, part.Refusal);
                            }
                            else
                            {
                                throw new Exception($"Unsupported content part kind: {part.Kind}");
                            }
                        }
                    }
                }
                yield return ChatSegment.Completed(new Dtos.ChatTokenUsage()
                {
                    InputTokens = response.Usage.InputTokenCount,
                    OutputTokens = response.Usage.OutputTokenCount,
                    ReasoningTokens = response.Usage.OutputTokenDetails.ReasoningTokenCount,
                }, hasTools ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop);
            }
        }
        else
        {
            await foreach (StreamingResponseUpdate delta in responseClient.CreateResponseStreamingAsync(ToResponse(messages), ToResponse(options), cancellationToken))
            {
                if (delta is StreamingResponseErrorUpdate error)
                {
                    string? errorMessage = ChatCompletionService.TryGetDecodedValue(ref error.Patch, "error"u8) ?? error.Message ?? "Unknown error";
                    throw new CustomChatServiceException(DBFinishReason.UpstreamError, errorMessage);
                }
                if (delta is StreamingResponseOutputTextDeltaUpdate textDelta)
                {
                    yield return ChatSegment.FromTextOnly(textDelta.Delta);
                }
                else if (delta is StreamingResponseCompletedUpdate completedDelta)
                {
                    yield return ChatSegment.Completed(new Dtos.ChatTokenUsage()
                    {
                        InputTokens = completedDelta.Response.Usage.InputTokenCount,
                        OutputTokens = completedDelta.Response.Usage.OutputTokenCount,
                        ReasoningTokens = completedDelta.Response.Usage.OutputTokenDetails.ReasoningTokenCount,
                    }, completedDelta.Response.Status switch
                    {
                        null => null,
                        ResponseStatus.Failed => ChatFinishReason.ContentFilter,
                        ResponseStatus.Completed => hasTools switch
                        {
                            true => ChatFinishReason.ToolCalls,
                            false => ChatFinishReason.Stop,
                        },
                        ResponseStatus.Incomplete => ChatFinishReason.Length,
                        _ => throw new NotSupportedException($"Unsupported response status: {completedDelta.Response.Status}"),
                    });
                }
                else if (delta is StreamingResponseOutputItemAddedUpdate addedDelta && addedDelta.Item is FunctionCallResponseItem fc)
                {
                    hasTools = true;
                    yield return ChatSegment.FromStartToolCall(addedDelta, fc);
                }
                else if (delta is StreamingResponseFunctionCallArgumentsDeltaUpdate fcDelta)
                {
                    yield return ChatSegment.FromToolCallDelta(fcDelta);
                }
                else if (delta is InternalResponseReasoningSummaryTextDeltaEvent rsDelta)
                {
                    yield return ChatSegment.FromThinkOnly(rsDelta.Delta.ToString());
                }
                else if (delta is InternalResponseReasoningSummaryTextDoneEvent rsDone)
                {
                    yield return ChatSegment.FromThinkOnly("\n\n");
                }
                else
                {
                    Console.WriteLine(delta.Kind.ToString());
                    //string type = DeltaTypeAccessor(delta);
                    //IDictionary<string, BinaryData>? said = GetSerializedAdditionalRawData(delta);

                    //if (type == "response.reasoning_summary_text.delta")
                    //{
                    //    string think = DeltaAccessor(delta);
                    //    yield return ChatSegment.FromThinkOnly(think);
                    //}
                    //else if (type == "response.reasoning_summary_text.done")
                    //{
                    //    yield return ChatSegment.FromThinkOnly("\n\n");
                    //}
                }
            }
        }
    }

    static List<ResponseItem> ToResponse(IReadOnlyList<ChatMessage> messages)
    {
        List<ResponseItem> responseItems = [];
        foreach (ChatMessage message in messages)
        {
            responseItems.AddRange(message switch
            {
                SystemChatMessage sys => [ResponseItem.CreateSystemMessageItem(MessageContentPartToResponse(sys.Content, input: true))],
                UserChatMessage user => [ResponseItem.CreateUserMessageItem(MessageContentPartToResponse(user.Content, input: true))],
                AssistantChatMessage assistant => AssistantChatMessageToResponse(assistant),
                DeveloperChatMessage developer => [ResponseItem.CreateDeveloperMessageItem(MessageContentPartToResponse(developer.Content, input: true))],
                ToolChatMessage tool => [ResponseItem.CreateFunctionCallOutputItem(tool.ToolCallId, string.Join("\r\n", tool.Content.Select(x => x.Text)))],
                _ => throw new NotSupportedException($"Unsupported message type: {message.GetType()}"),
            });
        }
        return responseItems;

        static IEnumerable<ResponseItem> AssistantChatMessageToResponse(AssistantChatMessage assistantChatMessage)
        {
            if (assistantChatMessage.ToolCalls != null && assistantChatMessage.ToolCalls.Count > 0)
            {
                foreach (ChatToolCall? toolCall in assistantChatMessage.ToolCalls)
                {
                    yield return ResponseItem.CreateFunctionCallItem(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments);
                }
            }
            if (assistantChatMessage.Content != null && assistantChatMessage.Content.Count > 0)
            {
                yield return ResponseItem.CreateAssistantMessageItem(MessageContentPartToResponse(assistantChatMessage.Content, input: false));
            }
        }

        static IReadOnlyList<ResponseContentPart> MessageContentPartToResponse(IReadOnlyList<ChatMessageContentPart> parts, bool input)
        {
            List<ResponseContentPart> responseParts = [];
            foreach (ChatMessageContentPart part in parts)
            {
                if (!input && part.Kind == ChatMessageContentPartKind.Image)
                {
                    // Response API does not support image content part in assistant message
                    responseParts.Add(ResponseContentPart.CreateOutputTextPart(part.ImageUri?.ToString() ?? "", []));
                    continue;
                }

                responseParts.Add(part.Kind switch
                {
                    ChatMessageContentPartKind.Text => input ? ResponseContentPart.CreateInputTextPart(part.Text) : ResponseContentPart.CreateOutputTextPart(part.Text, []),
                    ChatMessageContentPartKind.Image => part switch
                    {
                        { FileId: not null } => ResponseContentPart.CreateInputImagePart(part.FileId, ImageDetailLevelToResponse(part.ImageDetailLevel)),
                        { ImageUri: not null } => ResponseContentPart.CreateInputImagePart(part.ImageUri, ImageDetailLevelToResponse(part.ImageDetailLevel)),
                        { ImageBytes: not null } => ResponseContentPart.CreateInputImagePart(part.ImageBytes, part.ImageBytesMediaType, ImageDetailLevelToResponse(part.ImageDetailLevel)),
                        _ => throw new NotSupportedException($"Unsupported image content part: {part}"),
                    },
                    _ => throw new NotSupportedException($"Unsupported content part kind: {part.Kind}"),
                });
            }
            return responseParts;
        }

        static ResponseImageDetailLevel? ImageDetailLevelToResponse(ChatImageDetailLevel? level)
        {
            if (level == null)
            {
                return null;
            }

            if (level == ChatImageDetailLevel.Auto)
            {
                return ResponseImageDetailLevel.Auto;
            }
            else if (level == ChatImageDetailLevel.Low)
            {
                return ResponseImageDetailLevel.Low;
            }
            else if (level == ChatImageDetailLevel.High)
            {
                return ResponseImageDetailLevel.High;
            }
            else
            {
                throw new NotSupportedException($"Unsupported image detail level: {level}");
            }
        }
    }

    static ResponseCreationOptions ToResponse(ChatCompletionOptions options, bool background = false)
    {
        ResponseCreationOptions responseCreationOptions = new()
        {
            Temperature = options.Temperature,
            TopP = options.TopP,
            MaxOutputTokenCount = options.MaxOutputTokenCount,
            ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = options.ReasoningEffortLevel switch
                {
                    null => (ResponseReasoningEffortLevel?)null,
                    var x when x == "minimal" => new ResponseReasoningEffortLevel("minimal"),
                    var x when x == ChatReasoningEffortLevel.Low => ResponseReasoningEffortLevel.Low,
                    var x when x == ChatReasoningEffortLevel.Medium => ResponseReasoningEffortLevel.Medium,
                    var x when x == ChatReasoningEffortLevel.High => ResponseReasoningEffortLevel.High,
                    _ => throw new NotSupportedException($"Unsupported reasoning effort level: {options.ReasoningEffortLevel}"),
                },
                ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Detailed,
            },
            EndUserId = options.EndUserId,
            TextOptions = new ResponseTextOptions()
            {
                TextFormat = ToResponse(options.ResponseFormat)
            },
            ParallelToolCallsEnabled = options.AllowParallelToolCalls,
        };

        if (background)
        {
            responseCreationOptions.BackgroundModeEnabled = background;
        }

        foreach (ChatTool tool in options.Tools)
        {
            responseCreationOptions.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.FunctionName,
                tool.FunctionParameters,
                tool.FunctionSchemaIsStrict ?? false,
                tool.FunctionDescription));
        }

        return responseCreationOptions;

        static ResponseTextFormat ToResponse(ChatResponseFormat? format)
        {
            if (format == null) return ResponseTextFormat.CreateTextFormat();

            return format.GetType().Name switch
            {
                "InternalChatResponseFormatText" => ResponseTextFormat.CreateTextFormat(),
                "InternalDotNetChatResponseFormatJsonObject" => ResponseTextFormat.CreateJsonObjectFormat(),
                "InternalDotNetChatResponseFormatJsonSchema" => InternalChatResponseFormatJsonSchemaToResponse(format),
                _ => throw new NotSupportedException($"Unsupported response format: {format.GetType().Name}"),
            };

            static ResponseTextFormat InternalChatResponseFormatJsonSchemaToResponse(ChatResponseFormat format)
            {
                Type type = format.GetType();
                object? jsonSchema = (type.GetProperty("JsonSchema", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(format)) ?? throw new InvalidOperationException("JsonSchema property is null.");

                Type jsonSchemaType = jsonSchema.GetType();
                BinaryData binaryData = (BinaryData)jsonSchemaType.GetProperty("Schema")!.GetValue(jsonSchema)!;
                string? description = (string?)jsonSchemaType.GetProperty("Description")!.GetValue(jsonSchema);
                string name = (string)jsonSchemaType.GetProperty("Name")!.GetValue(jsonSchema)!;
                bool? strict = (bool?)jsonSchemaType.GetProperty("Strict")!.GetValue(jsonSchema);

                return ResponseTextFormat.CreateJsonSchemaFormat(name, binaryData, description, strict);
            }
        }
    }
}
