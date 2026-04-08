using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.RequestTracing;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.GoogleAI;

public class GoogleAI2ChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;

    private static readonly (string Category, string Threshold)[] DefaultSafetySettings =
    [
        ("HARM_CATEGORY_HATE_SPEECH", "BLOCK_NONE"),
        ("HARM_CATEGORY_SEXUALLY_EXPLICIT", "BLOCK_NONE"),
        ("HARM_CATEGORY_DANGEROUS_CONTENT", "BLOCK_NONE"),
        ("HARM_CATEGORY_HARASSMENT", "BLOCK_NONE"),
    ];

    internal const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta";

    public bool AllowImageGeneration(Model model) => model.DeploymentName.Contains("gemini-2.0-flash-exp") ||
                                                     model.DeploymentName.Contains("gemini-2.0-flash-exp-image-generation") ||
                                                     model.DeploymentName.Contains("gemini-2.5-flash-image");

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool allowImageGeneration = AllowImageGeneration(request.ChatConfig.Model);
        JsonObject requestBody = BuildNativeRequestBody(request, allowImageGeneration);

        string modelPath = NormalizeModelName(request.ChatConfig.Model.DeploymentName);
        string endpoint = $"{GetBaseUrl(request.ChatConfig.Model.ModelKey)}/{modelPath}:streamGenerateContent";

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, endpoint);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", request.ChatConfig.Model.ModelKey.Secret ?? throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "Google AI API key is required."));
        httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");

        using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceGemini);
        httpClient.Timeout = NetworkTimeout;

        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RawChatServiceException((int)response.StatusCode, errorBody);
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        int toolCallIndex = 0;
        string? codeExecutionId = null;
        Stopwatch codeExecutionSw = new();
        bool hasEmittedText = false; // Used to decide whether to discard late-arriving thoughtSignatures in plain text chats

        await foreach (JsonElement chunk in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(responseStream, JSON.JsonSerializerOptions, cancellationToken: cancellationToken))
        {
            if (chunk.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (chunk.TryGetProperty("error", out JsonElement errorElement))
            {
                string errorMessage = errorElement.TryGetProperty("message", out JsonElement messageElement)
                    ? messageElement.GetString() ?? errorElement.GetRawText()
                    : errorElement.GetRawText();
                throw new RawChatServiceException(200, errorMessage);
            }

            List<ChatSegment> items = [];
            DBFinishReason? finishReason = null;

            if (chunk.TryGetProperty("candidates", out JsonElement candidatesElement) &&
                candidatesElement.ValueKind == JsonValueKind.Array &&
                candidatesElement.GetArrayLength() > 0)
            {
                JsonElement candidate = candidatesElement[0];
                if (candidate.TryGetProperty("content", out JsonElement contentElement) &&
                    contentElement.TryGetProperty("parts", out JsonElement partsElement) &&
                    partsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement part in partsElement.EnumerateArray())
                    {
                        ProcessPart(part, items, ref toolCallIndex, ref codeExecutionId, ref codeExecutionSw, hasEmittedText);
                    }
                }

                if (candidate.TryGetProperty("finishReason", out JsonElement finishElement))
                {
                    finishReason = ToDbFinishReason(finishElement.GetString());
                }
            }

            ChatTokenUsage? usage = chunk.TryGetProperty("usageMetadata", out JsonElement usageElement) ? GetUsage(usageElement) : null;

            foreach (ChatSegment item in items)
            {
                if (item is TextChatSegment)
                {
                    hasEmittedText = true;
                }
                yield return item;
            }

            if (usage != null)
            {
                yield return ChatSegment.FromUsage(usage);
            }

            if (finishReason != null)
            {
                yield return ChatSegment.FromFinishReason(finishReason);
            }
        }
    }

    private static void ProcessPart(
        JsonElement part,
        List<ChatSegment> items,
        ref int toolCallIndex,
        ref string? codeExecutionId,
        ref Stopwatch codeExecutionSw,
        bool hasEmittedText)
    {
        if (part.TryGetProperty("thoughtSignature", out JsonElement signatureElement))
        {
            string? signature = signatureElement.GetString();
            if (!string.IsNullOrEmpty(signature))
            {
                bool isToolCall = part.TryGetProperty("functionCall", out _) || part.TryGetProperty("executableCode", out _);
                // Gemini will send a thoughtSignature after text parts in plain text chats.
                // We discard it to avoid showing an empty "thinking" block at the end of the message,
                // but we must keep it if it's associated with a tool call for multi-turn consistency.
                if (isToolCall || !hasEmittedText)
                {
                    items.Add(ChatSegment.FromThinkingSegment(NormalizeThoughtSignature(signature)));
                }
            }
        }

        if (part.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
        {
            string? text = textElement.GetString();
            if (!string.IsNullOrEmpty(text))
            {
                bool isThought = part.TryGetProperty("thought", out JsonElement thoughtElement) && thoughtElement.ValueKind == JsonValueKind.True;
                items.Add(isThought ? ChatSegment.FromThink(text) : ChatSegment.FromText(text));
            }
        }

        if (part.TryGetProperty("inlineData", out JsonElement inlineData) &&
            inlineData.TryGetProperty("data", out JsonElement dataElement))
        {
            string? base64 = dataElement.GetString();
            string mimeType = inlineData.TryGetProperty("mimeType", out JsonElement mimeElement) ? mimeElement.GetString() ?? "application/octet-stream" : "application/octet-stream";
            if (!string.IsNullOrEmpty(base64))
            {
                items.Add(ChatSegment.FromBase64Image(base64, mimeType));
            }
        }

        if (part.TryGetProperty("executableCode", out JsonElement executableCode))
        {
            string language = executableCode.TryGetProperty("language", out JsonElement languageElement) ? languageElement.GetString() ?? "code_execution" : "code_execution";
            string? code = executableCode.TryGetProperty("code", out JsonElement codeElement) ? codeElement.GetString() : null;
            if (code != null)
            {
                codeExecutionId = "ce-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                codeExecutionSw = Stopwatch.StartNew();
                string arguments = JsonSerializer.Serialize(new { code }, JSON.JsonSerializerOptions);
                items.Add(new ToolCallSegment
                {
                    Index = 0,
                    Id = codeExecutionId,
                    Name = language,
                    Arguments = arguments
                });
            }
        }

        if (part.TryGetProperty("codeExecutionResult", out JsonElement executionResult) && codeExecutionId != null)
        {
            string output = executionResult.TryGetProperty("output", out JsonElement outputElement) ? outputElement.GetString() ?? string.Empty : string.Empty;
            string outcome = executionResult.TryGetProperty("outcome", out JsonElement outcomeElement) ? outcomeElement.GetString() ?? string.Empty : string.Empty;
            bool isSuccess = string.Equals(outcome, "OUTCOME_OK", StringComparison.OrdinalIgnoreCase);
            int duration = codeExecutionSw.IsRunning ? (int)codeExecutionSw.ElapsedMilliseconds : 0;
            codeExecutionSw.Reset();

            items.Add(ChatSegment.FromToolCallResponse(codeExecutionId, output, duration, isSuccess: isSuccess));
            codeExecutionId = null;
        }

        if (part.TryGetProperty("functionCall", out JsonElement functionCall))
        {
            string? id = functionCall.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
            string? name = functionCall.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
            string arguments = "{}";
            if (functionCall.TryGetProperty("args", out JsonElement argsElement))
            {
                arguments = SerializeFunctionArgs(argsElement);
            }

            items.Add(new ToolCallSegment
            {
                Index = toolCallIndex,
                Id = id,
                Name = name,
                Arguments = arguments
            });
            toolCallIndex++;
        }
    }

    private static string SerializeFunctionArgs(JsonElement argsElement)
    {
        return argsElement.ValueKind switch
        {
            JsonValueKind.String => argsElement.GetString() ?? "{}",
            JsonValueKind.Null or JsonValueKind.Undefined => "{}",
            _ => argsElement.GetRawText()
        };
    }

    internal static ChatTokenUsage? GetUsage(JsonElement usageMetadata)
    {
        if (usageMetadata.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int promptTokens = usageMetadata.TryGetProperty("promptTokenCount", out JsonElement promptElement) ? promptElement.GetInt32() : 0;
        int totalTokens = usageMetadata.TryGetProperty("totalTokenCount", out JsonElement totalElement) ? totalElement.GetInt32() : 0; // not used
        int candidatesTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out JsonElement candidateElement) ? candidateElement.GetInt32() : 0;
        int reasoningTokens = usageMetadata.TryGetProperty("thoughtsTokenCount", out JsonElement reasoningElement) ? reasoningElement.GetInt32() : 0;
        int outputTokens = candidatesTokens + reasoningTokens;
        int usageTokens = usageMetadata.TryGetProperty("cachedContentTokenCount", out JsonElement cachedElement) ? cachedElement.GetInt32() : 0;

        return new ChatTokenUsage
        {
            InputTokens = promptTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            CacheTokens = usageTokens,
        };
    }

    private static DBFinishReason? ToDbFinishReason(string? finishReason)
    {
        if (string.IsNullOrEmpty(finishReason))
        {
            return null;
        }

        return finishReason.ToUpperInvariant() switch
        {
            "STOP" => DBFinishReason.Success,
            "MAX_TOKENS" => DBFinishReason.Length,
            "SAFETY" => DBFinishReason.ContentFilter,
            "RECITATION" => DBFinishReason.ContentFilter,
            "OTHER" => DBFinishReason.ContentFilter,
            "BLOCKLIST" => DBFinishReason.ContentFilter,
            "PROHIBITED_CONTENT" => DBFinishReason.ContentFilter,
            "SPII" => DBFinishReason.ContentFilter,
            "LANGUAGE" => DBFinishReason.ContentFilter,
            "IMAGE_SAFETY" => DBFinishReason.ContentFilter,
            "MALFORMED_FUNCTION_CALL" => DBFinishReason.ToolCalls,
            _ => null
        };
    }

    private JsonObject BuildNativeRequestBody(ChatRequest request, bool allowImageGeneration)
    {
        JsonObject body = new()
        {
            ["model"] = NormalizeModelName(request.ChatConfig.Model.DeploymentName),
            ["contents"] = ConvertMessages(request.Messages),
            ["safetySettings"] = BuildSafetySettings()
        };

        JsonObject? generationConfig = BuildGenerationConfig(request, allowImageGeneration);
        if (generationConfig != null && generationConfig.Count > 0)
        {
            body["generationConfig"] = generationConfig;
        }

        JsonObject? systemInstruction = BuildSystemInstruction(request.GetEffectiveSystemPrompt());
        if (systemInstruction != null)
        {
            body["systemInstruction"] = systemInstruction;
        }

        JsonArray? tools = BuildTools(request);
        if (tools != null && tools.Count > 0)
        {
            body["tools"] = tools;
        }

        return body;
    }

    private static JsonArray BuildSafetySettings()
    {
        JsonArray safety = [];
        foreach ((string Category, string Threshold) setting in DefaultSafetySettings)
        {
            safety.Add(new JsonObject
            {
                ["category"] = setting.Category,
                ["threshold"] = setting.Threshold
            });
        }
        return safety;
    }

    private static JsonObject? BuildGenerationConfig(ChatRequest request, bool allowImageGeneration)
    {
        JsonObject config = [];
        JsonArray modalities =
        [
            "TEXT"
        ];
        if (allowImageGeneration)
        {
            modalities.Add("IMAGE");
        }
        config["responseModalities"] = modalities;

        if (request.ChatConfig.Temperature is float temperature)
        {
            config["temperature"] = temperature;
        }

        if (request.TopP is float topP)
        {
            config["topP"] = topP;
        }

        int? maxTokens = request.ChatConfig.MaxOutputTokens ?? request.ChatConfig.Model.MaxResponseTokens;
        if (maxTokens.HasValue && maxTokens.Value > 0)
        {
            config["maxOutputTokens"] = maxTokens.Value;
        }

        if (!allowImageGeneration && !request.ChatConfig.Model.DeploymentName.Contains("2.5-pro", StringComparison.OrdinalIgnoreCase))
        {
            config["enableEnhancedCivicAnswers"] = true;
        }

        if (Model.GetReasoningEffortOptionsAsInt32(request.ChatConfig.Model.ReasoningEffortOptions).Length > 0)
        {
            JsonObject thinkingConfig = new()
            {
                ["includeThoughts"] = true
            };

            int? thinkingBudget = request.ChatConfig.ReasoningEffort switch
            {
                var effort when effort.IsLowOrMinimal() => 1024,
                _ => request.ChatConfig.ThinkingBudget
            };

            if (thinkingBudget.HasValue && thinkingBudget.Value > 0)
            {
                thinkingConfig["thinkingBudget"] = thinkingBudget.Value;
            }

            config["thinkingConfig"] = thinkingConfig;
        }

        return config;
    }

    private static JsonObject? BuildSystemInstruction(string? systemPrompt)
    {
        if (string.IsNullOrEmpty(systemPrompt))
        {
            return null;
        }

        return new JsonObject
        {
            ["role"] = "system",
            ["parts"] = new JsonArray
            {
                new JsonObject { ["text"] = systemPrompt }
            }
        };
    }

    private static JsonArray? BuildTools(ChatRequest request)
    {
        JsonArray tools = [];

        JsonObject? functionTool = BuildFunctionDeclarations(request);
        if (functionTool != null)
        {
            tools.Add(functionTool);
        }

        if (request.ModelProviderCodeExecutionEnabled && request.ChatConfig.Model.AllowCodeExecution)
        {
            tools.Add(new JsonObject
            {
                ["codeExecution"] = new JsonObject()
            });
        }

        if (request.ChatConfig.WebSearchEnabled && request.ChatConfig.Model.AllowSearch)
        {
            tools.Add(new JsonObject
            {
                ["googleSearch"] = new JsonObject()
            });
        }

        return tools.Count > 0 ? tools : null;
    }

    private static JsonObject? BuildFunctionDeclarations(ChatRequest request)
    {
        if (!request.ChatConfig.Model.AllowToolCall)
        {
            return null;
        }

        FunctionTool[] functionTools = request.Tools.OfType<FunctionTool>().ToArray();
        if (functionTools.Length == 0)
        {
            return null;
        }

        JsonArray declarations = [];
        foreach (FunctionTool tool in functionTools)
        {
            JsonObject declaration = new()
            {
                ["name"] = tool.FunctionName
            };

            if (!string.IsNullOrEmpty(tool.FunctionDescription))
            {
                declaration["description"] = tool.FunctionDescription;
            }

            declaration["parameters"] = BuildGoogleSchema(tool.FunctionParameters);
            declarations.Add(declaration);
        }

        return new JsonObject
        {
            ["functionDeclarations"] = declarations
        };
    }

    private static JsonObject BuildGoogleSchema(string? openAiSchema)
    {
        JsonObject schema = new()
        {
            ["type"] = "OBJECT"
        };

        if (string.IsNullOrEmpty(openAiSchema))
        {
            return schema;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(openAiSchema);
        }
        catch (JsonException)
        {
            return schema;
        }

        if (parsed is not JsonObject root)
        {
            return schema;
        }

        return ConvertSchema(root);
    }

    private static JsonObject ConvertSchema(JsonObject source)
    {
        JsonObject schema = new()
        {
            ["type"] = GetSchemaType(source, out bool isNullable)
        };

        if (isNullable)
        {
            schema["nullable"] = true;
        }

        if (TryGetStringValue(source, "description", out string? description))
        {
            schema["description"] = description;
        }

        if (source.TryGetPropertyValue("nullable", out JsonNode? nullableNode) && nullableNode is JsonValue nullableValue && nullableValue.TryGetValue(out bool explicitNullable) && explicitNullable)
        {
            schema["nullable"] = true;
        }

        if (source.TryGetPropertyValue("enum", out JsonNode? enumNode) && enumNode is JsonArray enumArray)
        {
            JsonArray convertedEnum = [];
            foreach (JsonNode? node in enumArray)
            {
                if (node is JsonValue enumValue && enumValue.TryGetValue(out string? enumItem) && enumItem != null)
                {
                    convertedEnum.Add(enumItem);
                }
            }

            if (convertedEnum.Count > 0)
            {
                schema["enum"] = convertedEnum;
            }
        }

        if (source.TryGetPropertyValue("items", out JsonNode? itemsNode) && itemsNode is JsonObject itemsObject)
        {
            schema["items"] = ConvertSchema(itemsObject);
        }

        if (source.TryGetPropertyValue("properties", out JsonNode? propsNode) && propsNode is JsonObject props)
        {
            JsonObject properties = [];
            foreach ((string key, JsonNode? value) in props)
            {
                if (value is JsonObject propertyObject)
                {
                    properties[key] = ConvertSchema(propertyObject);
                }
            }

            if (properties.Count > 0)
            {
                schema["properties"] = properties;
            }
        }

        CopyStringArrayProperty(source, schema, "required");
        CopyInt64Property(source, schema, "minItems");
        CopyInt64Property(source, schema, "maxItems");
        CopyInt64Property(source, schema, "minProperties");
        CopyInt64Property(source, schema, "maxProperties");
        CopyInt64Property(source, schema, "minLength");
        CopyInt64Property(source, schema, "maxLength");
        CopyNumberProperty(source, schema, "minimum");
        CopyNumberProperty(source, schema, "maximum");

        if (TryGetStringValue(source, "pattern", out string? pattern))
        {
            schema["pattern"] = pattern;
        }

        return schema;
    }

    private static string GetSchemaType(JsonObject source, out bool isNullable)
    {
        isNullable = false;

        if (source.TryGetPropertyValue("type", out JsonNode? typeNode))
        {
            if (typeNode is JsonValue typeValue && typeValue.TryGetValue(out string? singleType) && !string.IsNullOrWhiteSpace(singleType))
            {
                return ConvertSchemaType(singleType);
            }

            if (typeNode is JsonArray typeArray)
            {
                string? resolvedType = null;
                foreach (JsonNode? node in typeArray)
                {
                    if (node is not JsonValue value || !value.TryGetValue(out string? currentType) || string.IsNullOrWhiteSpace(currentType))
                    {
                        continue;
                    }

                    if (string.Equals(currentType, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        isNullable = true;
                        continue;
                    }

                    resolvedType ??= currentType;
                }

                if (resolvedType != null)
                {
                    return ConvertSchemaType(resolvedType);
                }

                if (isNullable)
                {
                    return "NULL";
                }
            }
        }

        if (source.ContainsKey("properties"))
        {
            return "OBJECT";
        }

        if (source.ContainsKey("items"))
        {
            return "ARRAY";
        }

        return "STRING";
    }

    private static bool TryGetStringValue(JsonObject source, string propertyName, out string? value)
    {
        value = null;

        if (source.TryGetPropertyValue(propertyName, out JsonNode? node) && node is JsonValue jsonValue && jsonValue.TryGetValue(out string? stringValue) && stringValue != null)
        {
            value = stringValue;
            return true;
        }

        return false;
    }

    private static void CopyStringArrayProperty(JsonObject source, JsonObject target, string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out JsonNode? node) && node is JsonArray sourceArray)
        {
            JsonArray targetArray = [];
            foreach (JsonNode? item in sourceArray)
            {
                if (item is JsonValue value && value.TryGetValue(out string? stringItem) && stringItem != null)
                {
                    targetArray.Add(stringItem);
                }
            }

            if (targetArray.Count > 0)
            {
                target[propertyName] = targetArray;
            }
        }
    }

    private static void CopyInt64Property(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out JsonNode? node) || node is not JsonValue value)
        {
            return;
        }

        if (value.TryGetValue(out long int64Value))
        {
            target[propertyName] = int64Value.ToString();
        }
    }

    private static void CopyNumberProperty(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out JsonNode? node) || node is not JsonValue value)
        {
            return;
        }

        if (value.TryGetValue(out decimal decimalValue))
        {
            target[propertyName] = decimalValue;
        }
    }

    private static string ConvertSchemaType(string? type)
    {
        return type switch
        {
            "integer" => "INTEGER",
            "null" => "NULL",
            "string" => "STRING",
            "number" => "NUMBER",
            "boolean" => "BOOLEAN",
            "array" => "ARRAY",
            "object" => "OBJECT",
            _ => "STRING"
        };
    }

    private static JsonArray ConvertMessages(IList<NeutralMessage> messages)
    {
        JsonArray result = [];
        foreach (NeutralMessage message in messages)
        {
            JsonObject content = new()
            {
                ["role"] = message.Role switch
                {
                    NeutralChatRole.User => "user",
                    NeutralChatRole.Assistant => "model",
                    NeutralChatRole.Tool => "function",
                    _ => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, $"Unsupported message role: {message.Role} in {nameof(GoogleAI2ChatService)}"),
                }
            };

            JsonArray parts = message.Role switch
            {
                NeutralChatRole.User => BuildUserParts(message),
                NeutralChatRole.Assistant => BuildAssistantParts(message),
                NeutralChatRole.Tool => BuildToolParts(message),
                _ => throw new NotSupportedException($"Unsupported message role: {message.Role} in {nameof(GoogleAI2ChatService)}"),
            };

            content["parts"] = parts;
            result.Add(content);
        }
        return result;
    }

    private static JsonArray BuildUserParts(NeutralMessage message)
    {
        JsonArray parts = [];
        foreach (NeutralContent content in message.Contents)
        {
            JsonObject? part = NeutralContentToGooglePart(content);
            if (part != null)
            {
                parts.Add(part);
            }
        }
        return parts;
    }

    private static JsonArray BuildAssistantParts(NeutralMessage message)
    {
        JsonArray parts = [];
        string? pendingThoughtSignature = null;

        foreach (NeutralContent content in message.Contents)
        {
            switch (content)
            {
                case NeutralThinkContent think:
                    // https://ai.google.dev/gemini-api/docs/thought-signatures?hl=zh-cn
                    // Don't need to care about the thinking content
                    //if (!string.IsNullOrEmpty(think.Content))
                    //{
                    //    parts.Add(new JsonObject
                    //    {
                    //        ["text"] = think.Content,
                    //        ["thought"] = true
                    //    });
                    //}
                    if (!string.IsNullOrEmpty(think.Signature))
                    {
                        pendingThoughtSignature = think.Signature;
                    }
                    break;

                case NeutralToolCallContent toolCall:
                    JsonObject args = ParseJson(toolCall.Parameters) as JsonObject ?? [];
                    JsonObject functionCall = new()
                    {
                        ["functionCall"] = new JsonObject
                        {
                            ["id"] = toolCall.Id,
                            ["name"] = toolCall.Name,
                            ["args"] = args
                        }
                    };
                    AttachPendingThoughtSignature(functionCall, ref pendingThoughtSignature);
                    parts.Add(functionCall);
                    break;

                default:
                    JsonObject? part = NeutralContentToGooglePart(content);
                    if (part != null)
                    {
                        AttachPendingThoughtSignature(part, ref pendingThoughtSignature);
                        parts.Add(part);
                    }
                    break;
            }
        }

        return parts;
    }

    private static void AttachPendingThoughtSignature(JsonObject part, ref string? pendingThoughtSignature)
    {
        if (pendingThoughtSignature != null)
        {
            part["thoughtSignature"] = pendingThoughtSignature;
            pendingThoughtSignature = null;
        }
    }

    private static JsonArray BuildToolParts(NeutralMessage message)
    {
        IReadOnlyList<NeutralToolResponseGroup> groups = message.GetToolResponseGroups();
        if (groups.Count == 0)
        {
            throw new CustomChatServiceException(DBFinishReason.BadParameter, $"{nameof(BuildToolParts)} expected tool call response content but none found.");
        }

        JsonArray parts = [];
        foreach (NeutralToolResponseGroup group in groups)
        {
            parts.Add(ToolCallMessageToPart(group));

            foreach (NeutralContent attachedContent in group.AttachedContents.Where(c => c is not NeutralFileBlobContent))
            {
                JsonObject? part = NeutralContentToGooglePart(attachedContent);
                if (part != null)
                {
                    parts.Add(part);
                }
            }
        }

        return parts;
    }

    private static JsonObject ToolCallMessageToPart(NeutralToolResponseGroup group)
    {
        JsonObject functionResponse = new()
        {
            ["name"] = group.ToolResponse.ToolCallId,
            ["response"] = new JsonObject
            {
                ["result"] = group.ToolResponse.Response,
                ["success"] = group.ToolResponse.IsSuccess
            }
        };

        JsonArray multimodalParts = BuildFunctionResponseParts(group.AttachedContents);
        if (multimodalParts.Count > 0)
        {
            functionResponse["parts"] = multimodalParts;
        }

        return new JsonObject
        {
            ["functionResponse"] = functionResponse
        };
    }

    private static JsonArray BuildFunctionResponseParts(IEnumerable<NeutralContent> contents)
    {
        JsonArray parts = [];
        foreach (NeutralFileBlobContent blob in contents.OfType<NeutralFileBlobContent>())
        {
            parts.Add(new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["data"] = Convert.ToBase64String(blob.Data),
                    ["mimeType"] = blob.MediaType
                }
            });
        }

        return parts;
    }

    private static JsonObject? NeutralContentToGooglePart(NeutralContent content)
    {
        return content switch
        {
            NeutralTextContent text => new JsonObject { ["text"] = text.Content },
            NeutralErrorContent error => new JsonObject { ["text"] = error.Content },
            NeutralFileBlobContent blob => new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["data"] = Convert.ToBase64String(blob.Data),
                    ["mimeType"] = blob.MediaType
                }
            },
            NeutralThinkContent => null,
            NeutralToolCallContent => null,
            NeutralToolCallResponseContent => null,
            NeutralFileUrlContent => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "FileUrl content is not supported for Google AI. Please convert to binary data first."),
            NeutralFileContent => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "File content should be materialized before sending to Google AI."),
            _ => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, $"Unsupported content type: {content.GetType().Name}")
        };
    }

    private static JsonNode? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }

    private static string NormalizeModelName(string deploymentName)
    {
        return deploymentName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? deploymentName
            : $"models/{deploymentName}";
    }

    private static string GetBaseUrl(ModelKey modelKey)
    {
        return string.IsNullOrWhiteSpace(modelKey.Host) ? DefaultEndpoint : modelKey.Host.TrimEnd('/');
    }

    private static string NormalizeThoughtSignature(string signature)
    {
        try
        {
            byte[] decoded = Convert.FromBase64String(signature);
            return Convert.ToBase64String(decoded);
        }
        catch (FormatException)
        {
            return signature;
        }
    }

    protected override string GetEndpoint(ModelKey modelKey)
    {
        // for gemini, this method is only used for extract models
        var url = base.GetEndpoint(modelKey).TrimEnd('/');
        if (!url.EndsWith("/openai"))
        {
            return url + "/openai";
        }
        return url;
    }
}
