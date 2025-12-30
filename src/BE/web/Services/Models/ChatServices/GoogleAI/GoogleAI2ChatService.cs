using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
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

        using HttpClient httpClient = httpClientFactory.CreateClient();
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

    private static ChatTokenUsage? GetUsage(JsonElement usageMetadata)
    {
        if (usageMetadata.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int promptTokens = usageMetadata.TryGetProperty("promptTokenCount", out JsonElement promptElement) ? promptElement.GetInt32() : 0;
        int totalTokens = usageMetadata.TryGetProperty("totalTokenCount", out JsonElement totalElement) ? totalElement.GetInt32() : 0;
        int outputTokens = totalTokens >= promptTokens ? totalTokens - promptTokens : 0;
        if (outputTokens == 0 && usageMetadata.TryGetProperty("candidatesTokenCount", out JsonElement candidateElement))
        {
            outputTokens = candidateElement.GetInt32();
        }
        int reasoningTokens = usageMetadata.TryGetProperty("thoughtsTokenCount", out JsonElement reasoningElement) ? reasoningElement.GetInt32() : 0;

        return new ChatTokenUsage
        {
            InputTokens = promptTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            CacheTokens = 0,
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

        if (root.TryGetPropertyValue("properties", out JsonNode? propsNode) && propsNode is JsonObject props)
        {
            JsonObject properties = [];
            foreach ((string key, JsonNode? value) in props)
            {
                if (value is not JsonObject propertyObject)
                {
                    continue;
                }

                JsonObject property = new()
                {
                    ["type"] = ConvertSchemaType(propertyObject.TryGetPropertyValue("type", out JsonNode? typeNode) ? typeNode?.GetValue<string>() : null)
                };

                if (propertyObject.TryGetPropertyValue("description", out JsonNode? descriptionNode) && descriptionNode is JsonValue descriptionValue && descriptionValue.TryGetValue(out string? description) && description != null)
                {
                    property["description"] = description;
                }

                properties[key] = property;
            }

            if (properties.Count > 0)
            {
                schema["properties"] = properties;
            }
        }

        if (root.TryGetPropertyValue("required", out JsonNode? requiredNode) && requiredNode is JsonArray requiredArray)
        {
            JsonArray required = [];
            foreach (JsonNode? node in requiredArray)
            {
                if (node is JsonValue value && value.TryGetValue(out string? str) && str != null)
                {
                    required.Add(str);
                }
            }
            if (required.Count > 0)
            {
                schema["required"] = required;
            }
        }

        return schema;
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
                NeutralChatRole.Tool => [ToolCallMessageToPart(message)],
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

    private static JsonObject ToolCallMessageToPart(NeutralMessage message)
    {
        NeutralToolCallResponseContent? responseContent = message.Contents.OfType<NeutralToolCallResponseContent>().FirstOrDefault();
        if (responseContent == null)
        {
            throw new CustomChatServiceException(DBFinishReason.BadParameter, $"{nameof(ToolCallMessageToPart)} expected tool call response content but none found.");
        }

        return new JsonObject
        {
            ["functionResponse"] = new JsonObject
            {
                ["name"] = responseContent.ToolCallId,
                ["response"] = new JsonObject
                {
                    ["result"] = ParseJson(responseContent.Response) ?? JsonValue.Create(responseContent.Response) ?? JsonValue.Create(string.Empty)!
                }
            }
        };
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
