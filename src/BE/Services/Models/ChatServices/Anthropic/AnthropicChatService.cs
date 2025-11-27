using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.Net.Http.Headers;
using ChatTokenUsage = Chats.BE.Services.Models.Dtos.ChatTokenUsage;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

public class AnthropicChatService(IHttpClientFactory httpClientFactory) : ChatService
{
    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        (string url, string apiKey) = GetEndpointAndKey(request.ChatConfig.Model.ModelKey);
        JsonObject requestBody = BuildRequestBody(request);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, url + "/v1/messages");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = NetworkTimeout;
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RawChatServiceException((int)response.StatusCode, errorBody);
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        int toolCallIndex = -1;
        await foreach (SseItem<string> sseItem in SseParser.Create(stream, (_, bytes) => Encoding.UTF8.GetString(bytes)).EnumerateAsync(cancellationToken))
        {
            if (string.IsNullOrEmpty(sseItem.Data) || sseItem.Data == "[DONE]")
            {
                continue;
            }

            JsonElement json;
            try
            {
                json = JsonDocument.Parse(sseItem.Data).RootElement;
            }
            catch (JsonException)
            {
                continue;
            }

            string? type = json.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() : null;

            switch (type)
            {
                case "message_start":
                    {
                        if (json.TryGetProperty("message", out JsonElement message) &&
                            message.TryGetProperty("usage", out JsonElement usage))
                        {
                            int inputTokens = usage.TryGetProperty("input_tokens", out JsonElement it) ? it.GetInt32() : 0;
                            int outputTokens = usage.TryGetProperty("output_tokens", out JsonElement ot) ? ot.GetInt32() : 0;
                            yield return ChatSegment.FromUsageOnly(inputTokens, outputTokens);
                        }
                        break;
                    }

                case "content_block_start":
                    {
                        if (json.TryGetProperty("content_block", out JsonElement contentBlock))
                        {
                            string? blockType = contentBlock.TryGetProperty("type", out JsonElement bt) ? bt.GetString() : null;

                            if (blockType == "tool_use")
                            {
                                ++toolCallIndex;
                                string? id = contentBlock.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                                string? name = contentBlock.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                                yield return ChatSegment.FromToolCall(new ToolCallSegment
                                {
                                    Arguments = "",
                                    Index = toolCallIndex,
                                    Id = id,
                                    Name = name,
                                });
                            }
                            else if (blockType == "server_tool_use")
                            {
                                ++toolCallIndex;
                                string? id = contentBlock.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                                string? name = contentBlock.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                                yield return ChatSegment.FromToolCall(new ToolCallSegment
                                {
                                    Arguments = "",
                                    Index = toolCallIndex,
                                    Id = id,
                                    Name = name,
                                });
                            }
                            else if (blockType == "web_search_tool_result")
                            {
                                string? toolUseId = contentBlock.TryGetProperty("tool_use_id", out JsonElement tuidEl) ? tuidEl.GetString() : null;
                                if (contentBlock.TryGetProperty("content", out JsonElement content) && toolUseId != null)
                                {
                                    string responseText = RemoveEncryptedContent(content);
                                    yield return ChatSegment.FromToolCallResponse(toolUseId, responseText);
                                }
                            }
                            // text block start - do nothing, wait for delta
                            // thinking block start - do nothing, wait for delta
                        }
                        break;
                    }

                case "content_block_delta":
                    {
                        if (json.TryGetProperty("delta", out JsonElement delta))
                        {
                            string? deltaType = delta.TryGetProperty("type", out JsonElement dt) ? dt.GetString() : null;

                            if (deltaType == "thinking_delta")
                            {
                                string? thinking = delta.TryGetProperty("thinking", out JsonElement th) ? th.GetString() : null;
                                if (!string.IsNullOrEmpty(thinking))
                                {
                                    yield return ChatSegment.FromThinking(thinking);
                                }
                            }
                            else if (deltaType == "text_delta")
                            {
                                string? text = delta.TryGetProperty("text", out JsonElement tx) ? tx.GetString() : null;
                                if (!string.IsNullOrEmpty(text))
                                {
                                    yield return ChatSegment.FromText(text);
                                }
                            }
                            else if (deltaType == "input_json_delta")
                            {
                                string? partialJson = delta.TryGetProperty("partial_json", out JsonElement pj) ? pj.GetString() : null;
                                yield return ChatSegment.FromToolCall(new ToolCallSegment
                                {
                                    Arguments = partialJson ?? "",
                                    Index = toolCallIndex,
                                });
                            }
                            else if (deltaType == "signature_delta")
                            {
                                string? signature = delta.TryGetProperty("signature", out JsonElement sig) ? sig.GetString() : null;
                                if (!string.IsNullOrEmpty(signature))
                                {
                                    yield return ChatSegment.FromThinkingSignature(signature);
                                }
                            }
                            // citations_delta - ignore for now
                        }
                        break;
                    }

                case "content_block_stop":
                    // no additional data needed
                    break;

                case "message_delta":
                    {
                        ChatFinishReason? finishReason = null;
                        if (json.TryGetProperty("delta", out JsonElement delta) &&
                            delta.TryGetProperty("stop_reason", out JsonElement stopReasonEl))
                        {
                            string? stopReason = stopReasonEl.GetString();
                            finishReason = stopReason switch
                            {
                                "end_turn" => ChatFinishReason.Stop,
                                "max_tokens" => ChatFinishReason.Length,
                                "stop_sequence" => ChatFinishReason.Stop,
                                "tool_use" => ChatFinishReason.ToolCalls,
                                "pause_turn" => ChatFinishReason.Stop,
                                "refusal" => ChatFinishReason.ContentFilter,
                                _ => null,
                            };
                        }

                        int inputTokens = 0;
                        int outputTokens = 0;
                        if (json.TryGetProperty("usage", out JsonElement usage))
                        {
                            inputTokens = usage.TryGetProperty("input_tokens", out JsonElement it) ? it.GetInt32() : 0;
                            outputTokens = usage.TryGetProperty("output_tokens", out JsonElement ot) ? ot.GetInt32() : 0;
                        }

                        yield return new ChatSegment
                        {
                            FinishReason = finishReason,
                            Items = [],
                            Usage = new ChatTokenUsage
                            {
                                InputTokens = inputTokens,
                                OutputTokens = outputTokens,
                            }
                        };
                        break;
                    }

                case "message_stop":
                    // ignore
                    break;

                case "ping":
                    // ignore
                    break;

                case "error":
                    {
                        throw new RawChatServiceException(200, sseItem.Data);
                    }
            }
        }
    }

    /// <summary>
    /// Removes the encrypted_content field from web search results as it's very long,
    /// cannot be understood by the model, and wastes storage/bandwidth.
    /// </summary>
    private static string RemoveEncryptedContent(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Array)
        {
            JsonArray results = [];
            foreach (JsonElement item in json.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    JsonObject obj = [];
                    foreach (JsonProperty prop in item.EnumerateObject())
                    {
                        if (prop.Name != "encrypted_content")
                        {
                            obj[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
                        }
                    }
                    results.Add(obj);
                }
                else
                {
                    results.Add(JsonNode.Parse(item.GetRawText()));
                }
            }
            return results.ToJsonString(JSON.JsonSerializerOptions);
        }
        return json.ToString();
    }

    protected virtual (string url, string apiKey) GetEndpointAndKey(ModelKey modelKey)
    {
        string url = (modelKey.Host ?? "https://api.anthropic.com").TrimEnd('/');
        if (url.EndsWith(".ai.azure.com")) // Azure AI Foundry Anthropic
        {
            url += "/anthropic";
        }
        return (url, modelKey.Secret ?? throw new InvalidOperationException("API key is required for Anthropic"));
    }

    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        (string url, string apiKey) = GetEndpointAndKey(modelKey);

        using HttpRequestMessage request = new(HttpMethod.Get, url + "/v1/models");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = NetworkTimeout;
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument doc = JsonDocument.Parse(json);

        List<string> models = [];
        if (doc.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement model in data.EnumerateArray())
            {
                if (model.TryGetProperty("id", out JsonElement id))
                {
                    string? modelId = id.GetString();
                    if (modelId != null)
                    {
                        models.Add(modelId);
                    }
                }
            }
        }
        return [.. models];
    }

    public override async Task<int> CountTokenAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        (string url, string apiKey) = GetEndpointAndKey(request.ChatConfig.Model.ModelKey);
        JsonObject requestBody = BuildCountTokensRequestBody(request);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, url + "/v1/messages/count_tokens");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");

        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = NetworkTimeout;
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("input_tokens", out JsonElement inputTokens))
        {
            return inputTokens.GetInt32();
        }

        return 0;
    }

    private static JsonObject BuildRequestBody(ChatRequest request)
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

        JsonObject body = new()
        {
            ["max_tokens"] = request.ChatConfig.Model.MaxResponseTokens,
            ["model"] = request.ChatConfig.Model.DeploymentName,
            ["messages"] = ConvertMessages(request.Steps, allowThinkingBlocks),
            ["stream"] = true,
        };

        if (request.ChatConfig.SystemPrompt != null)
        {
            body["system"] = request.ChatConfig.SystemPrompt;
        }

        if (request.ChatConfig.Temperature != null)
        {
            body["temperature"] = request.ChatConfig.Temperature.Value;
        }

        if (request.TopP != null)
        {
            body["top_p"] = request.TopP.Value;
        }

        if (allowThinking && request.ChatConfig.ThinkingBudget != null)
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "enabled",
                ["budget_tokens"] = request.ChatConfig.ThinkingBudget.Value
            };
        }

        JsonArray tools = BuildToolsArray(request);
        if (tools.Count > 0)
        {
            body["tools"] = tools;
        }

        return body;
    }

    private static JsonArray BuildToolsArray(ChatRequest request)
    {
        JsonArray tools = [];
        bool hasWebSearchTool = false;

        // Process tools from API
        foreach (ChatTool tool in request.Tools)
        {
            // Detect built-in web_search tool: name matches and no real parameters defined
            if (tool.FunctionName == "web_search" && !HasRealParameters(tool))
            {
                hasWebSearchTool = true;
                continue; // Will be added with correct format below
            }
            tools.Add(ConvertTool(tool));
        }

        // Add built-in web_search tool with correct format
        // Sources: API (hasWebSearchTool) or App config (WebSearchEnabled)
        if (hasWebSearchTool || request.ChatConfig.WebSearchEnabled)
        {
            tools.Add(new JsonObject
            {
                ["name"] = "web_search",
                ["type"] = "web_search_20250305"
            });
        }

        return tools;
    }

    private static bool HasRealParameters(ChatTool tool)
    {
        // Check if the tool has actual parameter definitions (not just empty schema)
        try
        {
            JsonObject? parameters = tool.FunctionParameters.ToObjectFromJson<JsonObject>();
            if (parameters == null) return false;

            // Has real parameters if "properties" exists and is non-empty
            if (parameters.TryGetPropertyValue("properties", out JsonNode? props) &&
                props is JsonObject propsObj && propsObj.Count > 0)
            {
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static JsonObject BuildCountTokensRequestBody(ChatRequest request)
    {
        bool hasThinkingBlocks = request.Steps
            .Where(s => s.ChatRole == DBChatRole.Assistant)
            .SelectMany(s => s.StepContents)
            .Any(sc => sc.ContentType == DBStepContentType.Think);

        bool allThinkingHaveSignature = !hasThinkingBlocks || request.Steps
            .Where(s => s.ChatRole == DBChatRole.Assistant)
            .SelectMany(s => s.StepContents)
            .Where(sc => sc.ContentType == DBStepContentType.Think)
            .All(sc => sc.StepContentThink?.Signature != null);

        bool allowThinkingBlocks = hasThinkingBlocks && allThinkingHaveSignature;
        bool hasToolCall = request.Steps.Any(m => m.ChatRole == DBChatRole.Assistant && m.StepContents.Any(sc => sc.ContentType == DBStepContentType.ToolCall));
        bool allowThinking = !hasToolCall || allowThinkingBlocks;

        JsonObject body = new()
        {
            ["model"] = request.ChatConfig.Model.DeploymentName,
            ["messages"] = ConvertMessages(request.Steps, allowThinkingBlocks),
        };

        if (request.ChatConfig.SystemPrompt != null)
        {
            body["system"] = request.ChatConfig.SystemPrompt;
        }

        if (allowThinking && request.ChatConfig.ThinkingBudget != null)
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "enabled",
                ["budget_tokens"] = request.ChatConfig.ThinkingBudget.Value
            };
        }

        JsonArray tools = BuildToolsArray(request);
        if (tools.Count > 0)
        {
            body["tools"] = tools;
        }

        return body;
    }

    private static JsonObject ConvertTool(ChatTool tool)
    {
        JsonObject inputSchema = new()
        {
            ["type"] = "object"
        };

        JsonObject? parameters = tool.FunctionParameters.ToObjectFromJson<JsonObject>();
        if (parameters != null)
        {
            if (parameters.TryGetPropertyValue("properties", out JsonNode? props) && props != null)
            {
                inputSchema["properties"] = JsonNode.Parse(props.ToJsonString());
            }
            if (parameters.TryGetPropertyValue("required", out JsonNode? req) && req != null)
            {
                inputSchema["required"] = JsonNode.Parse(req.ToJsonString());
            }
        }

        JsonObject result = new()
        {
            ["name"] = tool.FunctionName,
            ["input_schema"] = inputSchema
        };

        // description must be a valid string if present, cannot be null
        if (!string.IsNullOrEmpty(tool.FunctionDescription))
        {
            result["description"] = tool.FunctionDescription;
        }

        return result;
    }

    private static JsonArray ConvertMessages(IEnumerable<Step> messages, bool allowThinkingBlocks)
    {
        List<Step> mergedToolMessages = [.. SwitchServerToolResponsesAsUser(MergeToolMessages(messages))];
        JsonArray result = [];
        foreach (Step step in mergedToolMessages)
        {
            result.Add(ToAnthropicMessage(step, allowThinkingBlocks));
        }
        return result;
    }

    private static IEnumerable<Step> SwitchServerToolResponsesAsUser(IEnumerable<Step> steps)
    {
        // Anthropic requires tool responses to be in user messages
        // When response like web_search_tool_response comes back from server, we need to switch them to user role from assistant
        // For example: [user, assistant(think, tool, tool_response, text), user] -> [user, assistant(think, tool), user(tool_response), assistant(text), user]
        foreach (Step step in steps)
        {
            if (step.ChatRole != DBChatRole.Assistant)
            {
                yield return step;
                continue;
            }

            List<StepContent> assistantBuffer = [];
            List<StepContent> userBuffer = [];

            foreach (StepContent part in step.StepContents)
            {
                if (part.StepContentToolCallResponse is not null)
                {
                    // flush any accumulated assistant parts before emitting user tool responses
                    if (assistantBuffer.Count > 0)
                    {
                        yield return new Step
                        {
                            ChatRoleId = (byte)DBChatRole.Assistant,
                            StepContents = [.. assistantBuffer],
                        };
                        assistantBuffer.Clear();
                    }

                    userBuffer.Add(part);
                }
                else
                {
                    // if we have accumulated user tool responses, emit them first
                    if (userBuffer.Count > 0)
                    {
                        yield return new Step
                        {
                            ChatRoleId = (byte)DBChatRole.User,
                            StepContents = [.. userBuffer],
                        };
                        userBuffer.Clear();
                    }

                    assistantBuffer.Add(part);
                }
            }

            // flush remaining buffers in the original order
            if (assistantBuffer.Count > 0)
            {
                yield return new Step
                {
                    ChatRoleId = (byte)DBChatRole.Assistant,
                    StepContents = [.. assistantBuffer],
                };
            }

            if (userBuffer.Count > 0)
            {
                yield return new Step
                {
                    ChatRoleId = (byte)DBChatRole.User,
                    StepContents = [.. userBuffer],
                };
            }
        }
    }

    private static IEnumerable<Step> MergeToolMessages(IEnumerable<Step> messages)
    {
        // openai will omit tool messages, but anthropic needs them merged into the user message
        // for example:
        // openai: [user, assistant(request tool call, probably multiple), tool(tool response 1), tool(tool response 2), assistant]
        // anthropic: [user, assistant(request tool call, probably multiple), user(tool response 1 + 2), assistant]
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
                    yield return new Step
                    {
                        ChatRoleId = (byte)DBChatRole.User,
                        StepContents = [.. toolBuffer],
                    };
                    toolBuffer.Clear();
                }
                yield return message;
            }
        }

        if (toolBuffer.Count > 0)
        {
            yield return new Step
            {
                ChatRoleId = (byte)DBChatRole.User,
                StepContents = [.. toolBuffer],
            };
        }
    }

    private static JsonObject ToAnthropicMessage(Step message, bool allowThinkingBlocks)
    {
        string anthropicRole = message.ChatRole switch
        {
            DBChatRole.User => "user",
            DBChatRole.Assistant => "assistant",
            DBChatRole.ToolCall => throw new InvalidOperationException("Tool messages should be merged into user messages before conversion."),
            _ => throw new InvalidOperationException($"Unknown message type: {message.GetType().FullName}"),
        };

        JsonArray content = [];
        foreach (StepContent sc in message.StepContents)
        {
            JsonObject? contentBlock = ToAnthropicMessageContent(sc, allowThinkingBlocks);
            if (contentBlock != null)
            {
                content.Add(contentBlock);
            }
        }

        return new JsonObject
        {
            ["role"] = anthropicRole,
            ["content"] = content
        };
    }

    private static JsonObject? ToAnthropicMessageContent(StepContent part, bool allowThinkingBlocks)
    {
        if (part.TryGetTextPart(out string? text))
        {
            return new JsonObject
            {
                ["type"] = "text",
                ["text"] = text
            };
        }
        else if (part.TryGetFileUrl(out string? url))
        {
            return new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "url",
                    ["url"] = url
                }
            };
        }
        else if (part.TryGetFileBlob(out StepContentBlob? blob))
        {
            return new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = blob.MediaType,
                    ["data"] = Convert.ToBase64String(blob.Content)
                }
            };
        }
        else if (part.TryGetThink(out string? thinkText, out byte[]? signature))
        {
            if (allowThinkingBlocks)
            {
                string? signatureBase64 = signature != null ? Convert.ToBase64String(signature) : null;

                if (thinkText == null)
                {
                    return new JsonObject
                    {
                        ["type"] = "redacted_thinking",
                        ["data"] = signatureBase64
                    };
                }
                else
                {
                    return new JsonObject
                    {
                        ["type"] = "thinking",
                        ["thinking"] = thinkText,
                        ["signature"] = signatureBase64!
                    };
                }
            }
            else
            {
                return null; // drop invalid/unsigned or disallowed thinking blocks
            }
        }
        else if (part.TryGetError(out string? error))
        {
            return new JsonObject
            {
                ["type"] = "text",
                ["text"] = error
            };
        }
        else if (part.StepContentToolCall is not null)
        {
            StepContentToolCall toolCall = part.StepContentToolCall;
            return new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = toolCall.ToolCallId,
                ["name"] = toolCall.Name,
                ["input"] = JsonNode.Parse(toolCall.Parameters)
            };
        }
        else if (part.StepContentToolCallResponse is not null)
        {
            StepContentToolCallResponse toolResponse = part.StepContentToolCallResponse;
            JsonObject result = new()
            {
                ["type"] = "tool_result",
                ["tool_use_id"] = toolResponse.ToolCallId,
                ["content"] = toolResponse.Response
            };
            if (!toolResponse.IsSuccess)
            {
                result["is_error"] = true;
            }
            return result;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported StepContent type for Anthropic conversion: {(DBStepContentType)part.ContentTypeId}");
        }
    }
}
