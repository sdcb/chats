using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
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
                        DBFinishReason? finishReason = null;
                        if (json.TryGetProperty("delta", out JsonElement delta) &&
                            delta.TryGetProperty("stop_reason", out JsonElement stopReasonEl))
                        {
                            string? stopReason = stopReasonEl.GetString();
                            finishReason = stopReason switch
                            {
                                "end_turn" => DBFinishReason.Success,
                                "max_tokens" => DBFinishReason.Length,
                                "stop_sequence" => DBFinishReason.Success,
                                "tool_use" => DBFinishReason.ToolCalls,
                                "pause_turn" => DBFinishReason.Success,
                                "refusal" => DBFinishReason.ContentFilter,
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
        // Determine thinking block handling
        var (allowThinkingBlocks, allowThinking) = DetermineThinkingSettings(request);

        JsonObject body = new()
        {
            ["max_tokens"] = request.ChatConfig.Model.MaxResponseTokens,
            ["model"] = request.ChatConfig.Model.DeploymentName,
            ["messages"] = ConvertMessages(request.Messages, allowThinkingBlocks),
            ["stream"] = true,
        };

        // Handle system prompt with cache control support
        AddSystemPrompt(body, request);

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

    private static void AddSystemPrompt(JsonObject body, ChatRequest request)
    {
        // If we have a NeutralSystemMessage with cache control, use structured format
        if (request.System != null)
        {
            // Check if any content has cache control
            bool hasCacheControl = request.System.Contents.Any(c => c.CacheControl != null);

            if (hasCacheControl)
            {
                // Use structured array format for cache control support
                JsonArray systemArray = [];
                foreach (NeutralSystemContent content in request.System.Contents)
                {
                    JsonObject block = new()
                    {
                        ["type"] = "text",
                        ["text"] = content.Text
                    };
                    if (content.CacheControl != null)
                    {
                        block["cache_control"] = new JsonObject { ["type"] = content.CacheControl.Type };
                    }
                    systemArray.Add(block);
                }
                body["system"] = systemArray;
            }
            else
            {
                // Simple string format
                string? combined = request.System.GetCombinedText();
                if (combined != null)
                {
                    body["system"] = combined;
                }
            }
        }
        else if (request.ChatConfig.SystemPrompt != null)
        {
            // Fall back to simple string from ChatConfig
            body["system"] = request.ChatConfig.SystemPrompt;
        }
    }

    private static (bool allowThinkingBlocks, bool allowThinking) DetermineThinkingSettings(ChatRequest request)
    {
        // Anthropic has strict policies on thinking blocks
        bool hasThinkingBlocks = request.Messages
            .Where(m => m.Role == NeutralChatRole.Assistant)
            .SelectMany(m => m.Contents)
            .Any(c => c is NeutralThinkContent);

        bool allThinkingHaveSignature = !hasThinkingBlocks || request.Messages
            .Where(m => m.Role == NeutralChatRole.Assistant)
            .SelectMany(m => m.Contents)
            .OfType<NeutralThinkContent>()
            .All(tc => tc.Signature != null);

        bool allowThinkingBlocks = hasThinkingBlocks && allThinkingHaveSignature;

        bool hasToolCall = request.Messages.Any(m =>
            m.Role == NeutralChatRole.Assistant &&
            m.Contents.Any(c => c is NeutralToolCallContent));

        bool allowThinking = !hasToolCall || allowThinkingBlocks;

        return (allowThinkingBlocks, allowThinking);
    }

    private static JsonArray BuildToolsArray(ChatRequest request)
    {
        JsonArray tools = [];
        bool hasWebSearchTool = false;

        foreach (ChatTool tool in request.Tools)
        {
            if (tool.FunctionName == "web_search" && !HasRealParameters(tool))
            {
                hasWebSearchTool = true;
                continue;
            }
            tools.Add(ConvertTool(tool));
        }

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
        try
        {
            if (string.IsNullOrEmpty(tool.FunctionParameters)) return false;
            JsonObject? parameters = JsonSerializer.Deserialize<JsonObject>(tool.FunctionParameters);
            if (parameters == null) return false;

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
        var (allowThinkingBlocks, allowThinking) = DetermineThinkingSettings(request);

        JsonObject body = new()
        {
            ["model"] = request.ChatConfig.Model.DeploymentName,
            ["messages"] = ConvertMessages(request.Messages, allowThinkingBlocks),
        };

        AddSystemPrompt(body, request);

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
        JsonObject inputSchema = new() { ["type"] = "object" };

        JsonObject? parameters = string.IsNullOrEmpty(tool.FunctionParameters) ? null : JsonSerializer.Deserialize<JsonObject>(tool.FunctionParameters);
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

        if (!string.IsNullOrEmpty(tool.FunctionDescription))
        {
            result["description"] = tool.FunctionDescription;
        }

        return result;
    }

    private static JsonArray ConvertMessages(IList<NeutralMessage> messages, bool allowThinkingBlocks)
    {
        List<NeutralMessage> mergedMessages = [.. SwitchServerToolResponsesAsUser(MergeToolMessages(messages))];
        JsonArray result = [];
        foreach (NeutralMessage msg in mergedMessages)
        {
            result.Add(ToAnthropicMessage(msg, allowThinkingBlocks));
        }
        return result;
    }

    private static IEnumerable<NeutralMessage> SwitchServerToolResponsesAsUser(IEnumerable<NeutralMessage> messages)
    {
        foreach (NeutralMessage msg in messages)
        {
            if (msg.Role != NeutralChatRole.Assistant)
            {
                yield return msg;
                continue;
            }

            List<NeutralContent> assistantBuffer = [];
            List<NeutralContent> userBuffer = [];

            foreach (NeutralContent content in msg.Contents)
            {
                if (content is NeutralToolCallResponseContent)
                {
                    if (assistantBuffer.Count > 0)
                    {
                        yield return new NeutralMessage
                        {
                            Role = NeutralChatRole.Assistant,
                            Contents = [.. assistantBuffer],
                        };
                        assistantBuffer.Clear();
                    }
                    userBuffer.Add(content);
                }
                else
                {
                    if (userBuffer.Count > 0)
                    {
                        yield return new NeutralMessage
                        {
                            Role = NeutralChatRole.User,
                            Contents = [.. userBuffer],
                        };
                        userBuffer.Clear();
                    }
                    assistantBuffer.Add(content);
                }
            }

            if (assistantBuffer.Count > 0)
            {
                yield return new NeutralMessage
                {
                    Role = NeutralChatRole.Assistant,
                    Contents = [.. assistantBuffer],
                };
            }

            if (userBuffer.Count > 0)
            {
                yield return new NeutralMessage
                {
                    Role = NeutralChatRole.User,
                    Contents = [.. userBuffer],
                };
            }
        }
    }

    private static IEnumerable<NeutralMessage> MergeToolMessages(IEnumerable<NeutralMessage> messages)
    {
        List<NeutralContent> toolBuffer = [];

        foreach (NeutralMessage msg in messages)
        {
            if (msg.Role == NeutralChatRole.Tool)
            {
                toolBuffer.AddRange(msg.Contents);
            }
            else
            {
                if (toolBuffer.Count > 0)
                {
                    yield return new NeutralMessage
                    {
                        Role = NeutralChatRole.User,
                        Contents = [.. toolBuffer],
                    };
                    toolBuffer.Clear();
                }
                yield return msg;
            }
        }

        if (toolBuffer.Count > 0)
        {
            yield return new NeutralMessage
            {
                Role = NeutralChatRole.User,
                Contents = [.. toolBuffer],
            };
        }
    }

    private static JsonObject ToAnthropicMessage(NeutralMessage message, bool allowThinkingBlocks)
    {
        string anthropicRole = message.Role switch
        {
            NeutralChatRole.User => "user",
            NeutralChatRole.Assistant => "assistant",
            NeutralChatRole.Tool => throw new InvalidOperationException("Tool messages should be merged into user messages before conversion."),
            _ => throw new InvalidOperationException($"Unknown message role: {message.Role}"),
        };

        JsonArray content = [];
        foreach (NeutralContent c in message.Contents)
        {
            JsonObject? contentBlock = ToAnthropicContent(c, allowThinkingBlocks);
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

    private static JsonObject? ToAnthropicContent(NeutralContent content, bool allowThinkingBlocks)
    {
        JsonObject? result = content switch
        {
            NeutralTextContent text => new JsonObject { ["type"] = "text", ["text"] = text.Content },
            NeutralErrorContent error => new JsonObject { ["type"] = "text", ["text"] = error.Content },
            NeutralFileUrlContent fileUrl => new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject { ["type"] = "url", ["url"] = fileUrl.Url }
            },
            NeutralFileBlobContent fileBlob => new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = fileBlob.MediaType,
                    ["data"] = Convert.ToBase64String(fileBlob.Data)
                }
            },
            NeutralThinkContent think when allowThinkingBlocks => CreateThinkingBlock(think),
            NeutralThinkContent => null, // Drop thinking blocks when not allowed
            NeutralToolCallContent toolCall => new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = toolCall.Id,
                ["name"] = toolCall.Name,
                ["input"] = JsonNode.Parse(toolCall.Parameters)
            },
            NeutralToolCallResponseContent toolResp => CreateToolResultBlock(toolResp),
            NeutralFileContent => throw new InvalidOperationException("FileId should be converted to FileUrl/FileBlob before conversion."),
            _ => throw new InvalidOperationException($"Unsupported content type: {content.GetType().Name}")
        };

        // Add cache control if present
        if (result != null && content.CacheControl != null)
        {
            result["cache_control"] = new JsonObject { ["type"] = content.CacheControl.Type };
        }

        return result;
    }

    private static JsonObject CreateThinkingBlock(NeutralThinkContent think)
    {
        string? signatureBase64 = think.Signature != null ? Convert.ToBase64String(think.Signature) : null;

        if (string.IsNullOrEmpty(think.Content))
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
                ["thinking"] = think.Content,
                ["signature"] = signatureBase64!
            };
        }
    }

    private static JsonObject CreateToolResultBlock(NeutralToolCallResponseContent toolResp)
    {
        JsonObject result = new()
        {
            ["type"] = "tool_result",
            ["tool_use_id"] = toolResp.ToolCallId,
            ["content"] = toolResp.Response
        };
        if (!toolResp.IsSuccess)
        {
            result["is_error"] = true;
        }
        return result;
    }
}
