using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ResponseApiService(ILogger<ResponseApiService> logger) : ChatService
{
    protected virtual OpenAIResponseClient CreateResponseAPI(Model model, PipelinePolicy[] pipelinePolicies)
    {
        OpenAIClient api = CreateOpenAIClient(model.ModelKey, pipelinePolicies);
        OpenAIResponseClient cc = api.GetOpenAIResponseClient(model.DeploymentName);
        return cc;
    }

    protected virtual OpenAIClient CreateOpenAIClient(ModelKey modelKey, params PipelinePolicy[] perCallPolicies)
    {
        return OpenAIHelper.BuildOpenAIClient(modelKey, perCallPolicies);
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        OpenAIResponseClient responseClient = CreateResponseAPI(request.ChatConfig.Model, []);

        bool hasTools = false;
        if (request.ChatConfig.Model.UseAsyncApi)
        {
            Stopwatch sw = Stopwatch.StartNew();
            OpenAIResponse response = await responseClient.CreateResponseAsync(ExtractMessages(request), ExtractOptions(request, background: true), cancellationToken);

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
                    response = await responseClient.GetResponseAsync(response.Id, cancellationToken: cancellationToken);
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
                    FinishReason = null,
                    Items = [],
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
                            yield return ChatSegment.FromThinking(string.Join(
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
                                yield return ChatSegment.FromText(part.Text);
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
            await foreach (StreamingResponseUpdate delta in responseClient.CreateResponseStreamingAsync(ExtractMessages(request), ExtractOptions(request), cancellationToken))
            {
                if (delta is StreamingResponseErrorUpdate error)
                {
                    string? errorMessage = ChatCompletionService.TryGetDecodedValue(ref error.Patch, "error"u8) ?? error.Message ?? "Unknown error";
                    throw new CustomChatServiceException(DBFinishReason.UpstreamError, errorMessage);
                }
                if (delta is StreamingResponseOutputTextDeltaUpdate textDelta)
                {
                    yield return ChatSegment.FromText(textDelta.Delta);
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
                else if (delta is StreamingResponseReasoningSummaryTextDeltaUpdate rsDelta)
                {
                    yield return ChatSegment.FromThinking(rsDelta.Delta.ToString());
                }
                else if (delta is StreamingResponseReasoningSummaryTextDoneUpdate rsDone)
                {
                    yield return ChatSegment.FromThinking("\n\n");
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

    static List<ResponseItem> ExtractMessages(ChatRequest request)
    {
        string? effectiveSystemPrompt = request.GetEffectiveSystemPrompt();
        List<ResponseItem> responseItems = new(request.Messages.Count + (effectiveSystemPrompt != null ? 1 : 0));
        if (effectiveSystemPrompt != null)
        {
            responseItems.Add(ResponseItem.CreateSystemMessageItem(effectiveSystemPrompt));
        }

        foreach (NeutralMessage message in request.Messages)
        {
            responseItems.AddRange(message.Role switch
            {
                NeutralChatRole.User => [ResponseItem.CreateUserMessageItem(MessageContentPartToResponse(message.Contents, input: true))],
                NeutralChatRole.Assistant => AssistantMessageToResponse(message),
                NeutralChatRole.Tool => [ResponseItem.CreateFunctionCallOutputItem(
                    ((NeutralToolCallResponseContent)message.Contents.First()).ToolCallId,
                    ((NeutralToolCallResponseContent)message.Contents.First()).Response)],
                _ => throw new NotSupportedException($"Unsupported message role: {message.Role}"),
            });
        }
        return responseItems;

        static IEnumerable<ResponseItem> AssistantMessageToResponse(NeutralMessage assistantMessage)
        {
            foreach (NeutralToolCallContent tc in assistantMessage.Contents.OfType<NeutralToolCallContent>())
            {
                yield return ResponseItem.CreateFunctionCallItem(tc.Id, tc.Name, BinaryData.FromString(tc.Parameters));
            }
            List<NeutralContent> generalContents = [.. assistantMessage.Contents.Where(x => x is not NeutralToolCallContent)];
            if (generalContents.Count > 0)
            {
                yield return ResponseItem.CreateAssistantMessageItem(MessageContentPartToResponse(generalContents, input: false));
            }
        }

        static IReadOnlyList<ResponseContentPart> MessageContentPartToResponse(IList<NeutralContent> contents, bool input)
        {
            return contents
                .Select(x => NeutralContentToResponsePart(x, input))
                .Where(x => x != null)
                .ToList()!;
        }

        static ResponseContentPart? NeutralContentToResponsePart(NeutralContent content, bool input)
        {
            if (input)
            {
                return content switch
                {
                    NeutralTextContent text => ResponseContentPart.CreateInputTextPart(text.Content),
                    NeutralFileUrlContent fileUrl => ResponseContentPart.CreateInputImagePart(new Uri(fileUrl.Url)),
                    NeutralFileBlobContent blob => ResponseContentPart.CreateInputImagePart(BinaryData.FromBytes(blob.Data), blob.MediaType),
                    NeutralErrorContent error => ResponseContentPart.CreateInputTextPart(error.Content),
                    _ => throw new NotSupportedException($"Unsupported input content type: {content.GetType().Name}"),
                };
            }
            else
            {
                return content switch
                {
                    NeutralTextContent text => ResponseContentPart.CreateOutputTextPart(text.Content, []),
                    NeutralFileUrlContent fileUrl => ResponseContentPart.CreateOutputTextPart(fileUrl.Url, []),
                    NeutralThinkContent => null, // Response API does not support think parts in output
                    NeutralFileBlobContent => throw new NotSupportedException("File blob is not supported for output content"),
                    NeutralErrorContent error => ResponseContentPart.CreateOutputTextPart(error.Content, []),
                    NeutralToolCallContent => null, // Tool calls are handled separately
                    NeutralToolCallResponseContent => null, // Tool call responses are handled separately
                    _ => throw new NotSupportedException($"Unsupported output content type: {content.GetType().Name}"),
                };
            }
        }
    }

    static ResponseCreationOptions ExtractOptions(ChatRequest request, bool background = false)
    {
        ResponseCreationOptions responseCreationOptions = new()
        {
            Temperature = request.ChatConfig.Temperature,
            //TopP = options.TopP, // Not supported in ChatConfig for now
            MaxOutputTokenCount = request.ChatConfig.MaxOutputTokens,
            ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = request.ChatConfig.ReasoningEffort.ToResponseReasoningEffort(),
                ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Detailed,
            },
            EndUserId = request.EndUserId,
            TextOptions = new ResponseTextOptions()
            {
                TextFormat = ToResponse(request.TextFormat)
            },
            ParallelToolCallsEnabled = request.AllowParallelToolCalls,
        };

        if (background)
        {
            responseCreationOptions.BackgroundModeEnabled = background;
        }

        foreach (ChatTool tool in request.Tools)
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

    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        OpenAIClient api = CreateOpenAIClient(modelKey, []);
        ClientResult<OpenAIModelCollection> result = await api.GetOpenAIModelClient().GetModelsAsync(cancellationToken);
        return [.. result.Value.Select(m => m.Id)];
    }
}
