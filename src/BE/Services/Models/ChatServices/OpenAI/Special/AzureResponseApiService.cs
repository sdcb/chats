using Azure.AI.OpenAI;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class AzureResponseApiService(Model model) : ChatService(model)
{
    static OpenAIResponseClient CreateResponseAPI(Model model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Host, nameof(model.ModelKey.Host));
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelKey.Secret, nameof(model.ModelKey.Secret));

        AzureOpenAIClientOptions options = new()
        {
            NetworkTimeout = NetworkTimeout,
        };

        OpenAIClient api = new AzureOpenAIClient(
            new Uri(model.ModelKey.Host),
            new ApiKeyCredential(model.ModelKey.Secret), options);
        OpenAIResponseClient cc = api.GetOpenAIResponseClient(model.ApiModelId);
        SetApiVersion(cc, "2025-04-01-preview");
        return cc;

        static void SetApiVersion(OpenAIResponseClient api, string version)
        {
            FieldInfo? versionField = api.GetType().GetField("_apiVersion", BindingFlags.NonPublic | BindingFlags.Instance) 
                ?? throw new InvalidOperationException("Unable to access the API version field.");
            versionField.SetValue(api, version);
        }
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        OpenAIResponseClient api = CreateResponseAPI(Model);
        bool hasTools = false;
        await foreach (StreamingResponseUpdate delta in api.CreateResponseStreamingAsync(ToResponse(messages), ToResponse(options), cancellationToken))
        {
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
            else
            {
                string type = DeltaTypeAccessor(delta);
                IDictionary<string, BinaryData>? said = GetSerializedAdditionalRawData(delta);

                if (type == "response.reasoning_summary_text.delta" && said != null && said.TryGetValue("delta", out BinaryData? deltaBinary))
                {
                    string? think = deltaBinary.ToObjectFromJson<string>();
                    if (!string.IsNullOrEmpty(think))
                    {
                        yield return ChatSegment.FromThinkOnly(think);
                    }
                }
                else if (type == "response.reasoning_summary_text.done")
                {
                    yield return ChatSegment.FromThinkOnly("\n\n");
                }
            }
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_additionalBinaryDataProperties")]
    private extern static ref IDictionary<string, BinaryData>? GetSerializedAdditionalRawData(StreamingResponseUpdate @this);

    private readonly static Func<StreamingResponseUpdate, string> DeltaTypeAccessor = CreateDeltaTypeAccessor();

    static Func<StreamingResponseUpdate, string> CreateDeltaTypeAccessor()
    {
        Type type = typeof(StreamingResponseUpdate);
        PropertyInfo? deltaTypeProperty = type.GetProperty("Type", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Unable to access the Type property.");
        FieldInfo field = deltaTypeProperty.PropertyType.GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to access the _value field.");
        if (field.FieldType != typeof(string))
        {
            throw new InvalidOperationException($"The field type is not string: {field.FieldType}");
        }

        return (delta) =>
        {
            object prop = deltaTypeProperty.GetValue(delta) ?? throw new InvalidOperationException("The Type property is null.");
            string type = field.GetValue(prop) as string ?? throw new InvalidOperationException("The _value field is null.");
            return type;
        };
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
                foreach (var toolCall in assistantChatMessage.ToolCalls)
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
                        { ImageUri: not null } => ResponseContentPart.CreateInputImagePart(part.ImageUri.ToString(), ImageDetailLevelToResponse(part.ImageDetailLevel)),
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

    protected override void SetReasoningEffort(ChatCompletionOptions options, DBReasoningEffort reasoningEffort)
    {
        // override in ToResponse
    }

    static ResponseCreationOptions ToResponse(ChatCompletionOptions options)
    {
        ResponseCreationOptions responseCreationOptions = new()
        {
            Temperature = options.Temperature,
            TopP = options.TopP,
            MaxOutputTokenCount = options.MaxOutputTokenCount,
            ReasoningOptions = new MyResponseReasoningOptions()
            {
                ReasoningEffortLevel = options.ReasoningEffortLevel switch
                {
                    null => (ResponseReasoningEffortLevel?)null,
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
                TextFormat = ToResponse(options.ResponseFormat),
            },
            ParallelToolCallsEnabled = options.AllowParallelToolCalls,
        };

        foreach (ChatTool tool in options.Tools)
        {
            responseCreationOptions.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.FunctionName,
                tool.FunctionDescription,
                tool.FunctionParameters,
                tool.FunctionSchemaIsStrict ?? false));
        }

        return responseCreationOptions;

        static ResponseTextFormat ToResponse(ChatResponseFormat? format)
        {
            if (format == null) return ResponseTextFormat.CreateTextFormat();

            return format.GetType().Name switch
            {
                "InternalChatResponseFormatText" => ResponseTextFormat.CreateTextFormat(),
                "InternalChatResponseFormatJsonObject" => ResponseTextFormat.CreateJsonObjectFormat(),
                "InternalChatResponseFormatJsonSchema" => InternalChatResponseFormatJsonSchemaToResponse(format),
                _ => throw new NotSupportedException($"Unsupported response format: {format.GetType().Name}"),
            };

            static ResponseTextFormat InternalChatResponseFormatJsonSchemaToResponse(ChatResponseFormat format)
            {
                Type type = format.GetType();
                object? jsonSchema = (type.GetProperty("JsonSchema")?.GetValue(format)) ?? throw new InvalidOperationException("JsonSchema property is null.");

                Type jsonSchemaType = jsonSchema.GetType();
                BinaryData binaryData = (BinaryData)jsonSchemaType.GetProperty("Schema")!.GetValue(jsonSchema)!;
                string? description = (string?)jsonSchemaType.GetProperty("Description")!.GetValue(jsonSchema);
                string name = (string)jsonSchemaType.GetProperty("Name")!.GetValue(jsonSchema)!;
                bool? strict = (bool?)jsonSchemaType.GetProperty("Strict")!.GetValue(jsonSchema);

                return ResponseTextFormat.CreateJsonSchemaFormat(name, binaryData, description, strict);
            }
        }
    }

    private class MyResponseReasoningOptions : ResponseReasoningOptions
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_additionalBinaryDataProperties")]
        public static extern ref IDictionary<string, BinaryData>? GetSerializedAdditionalRawData(ResponseReasoningOptions instance);

        protected override void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ResponseReasoningOptions>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ResponseReasoningOptions)} does not support writing '{format}' format.");
            }
            ref IDictionary<string, BinaryData>? _additionalBinaryDataProperties = ref GetSerializedAdditionalRawData(this);
            //_additionalBinaryDataProperties ??= new Dictionary<string, BinaryData>();

            if (_additionalBinaryDataProperties?.ContainsKey("effort") != true)
            {
                if (ReasoningEffortLevel != null)
                {
                    writer.WritePropertyName("effort"u8);
                    writer.WriteStringValue(ReasoningEffortLevel.Value.ToString());
                }
                else
                {
                    writer.WriteNull("effort"u8);
                }
            }
            if (ReasoningSummaryVerbosity != null && _additionalBinaryDataProperties?.ContainsKey("summary") != true)
            {
                writer.WritePropertyName("summary"u8);
                writer.WriteStringValue(ReasoningSummaryVerbosity.Value.ToString());
            }
            if (_additionalBinaryDataProperties != null)
            {
                foreach (var item in _additionalBinaryDataProperties)
                {
                    if ("\"__EMPTY__\""u8.SequenceEqual(item.Value.ToMemory().Span))
                    {
                        continue;
                    }
                    writer.WritePropertyName(item.Key);
                    writer.WriteRawValue(item.Value);
                }
            }
        }
    }
}
