using Chats.DB;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.RequestTracing;
using System.Net.Http.Headers;
using ChatTokenUsage = Chats.BE.Services.Models.Dtos.ChatTokenUsage;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.DB.Enums;

namespace Chats.BE.Services.Models.ChatServices.Anthropic;

public class AnthropicChatService(IHttpClientFactory httpClientFactory) : ChatService
{
    protected virtual bool SupportsHostedWebSearch => false;
    protected virtual bool SupportsRedactedThinking => true;

    private sealed class HostedWebSearchCallState
    {
        public required int SegmentIndex { get; init; }
        public required JsonObject Block { get; init; }
        public StringBuilder PartialInput { get; } = new();
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Model model = request.GetRequiredModel();
        (string url, string apiKey) = GetMessagesEndpointAndKey(model.CurrentSnapshot);
        JsonObject requestBody = BuildRequestBody(request);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, url);
        AddApiKeyHeader(httpRequest, apiKey);
        httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceAnthropic);
        httpClient.Timeout = NetworkTimeout;
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await RawChatServiceException.CreateAsync(response, cancellationToken);
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        int nextToolCallIndex = 0;
        Dictionary<int, int> toolCallIndexes = [];
        Dictionary<int, HostedWebSearchCallState> hostedWebSearchCalls = [];
        ChatTokenUsage? lastKnownUsage = null;
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
                            lastKnownUsage = MergeUsage(lastKnownUsage, usage);
                            yield return ChatSegment.FromUsage(lastKnownUsage);
                        }
                        break;
                    }

                case "content_block_start":
                    {
                        if (json.TryGetProperty("content_block", out JsonElement contentBlock))
                        {
                            int blockIndex = json.TryGetProperty("index", out JsonElement indexEl) ? indexEl.GetInt32() : -1;
                            string? blockType = contentBlock.TryGetProperty("type", out JsonElement bt) ? bt.GetString() : null;

                            if (blockType == "tool_use")
                            {
                                int toolCallIndex = nextToolCallIndex++;
                                toolCallIndexes[blockIndex] = toolCallIndex;
                                string? id = contentBlock.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                                string? name = contentBlock.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                                yield return new ToolCallSegment
                                {
                                    Arguments = "",
                                    Index = toolCallIndex,
                                    Id = id,
                                    Name = name,
                                };
                            }
                            else if (SupportsHostedWebSearch && blockType == DeepSeekHostedWebSearch.ServerToolUseType)
                            {
                                JsonObject? rawBlock = JsonNode.Parse(contentBlock.GetRawText()) as JsonObject;
                                if (rawBlock != null)
                                {
                                    hostedWebSearchCalls[blockIndex] = new HostedWebSearchCallState
                                    {
                                        SegmentIndex = nextToolCallIndex++,
                                        Block = rawBlock,
                                    };
                                }
                            }
                            else if (SupportsHostedWebSearch && blockType == DeepSeekHostedWebSearch.ToolResultType)
                            {
                                string? toolUseId = contentBlock.TryGetProperty("tool_use_id", out JsonElement tuidEl) ? tuidEl.GetString() : null;
                                if (toolUseId != null)
                                {
                                    yield return ChatSegment.FromToolCallResponse(toolUseId, contentBlock.GetRawText(), 0, true);
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
                                    yield return ChatSegment.FromThink(thinking);
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
                                int blockIndex = json.TryGetProperty("index", out JsonElement indexEl) ? indexEl.GetInt32() : -1;
                                if (hostedWebSearchCalls.TryGetValue(blockIndex, out HostedWebSearchCallState? hostedCall))
                                {
                                    hostedCall.PartialInput.Append(partialJson);
                                }
                                else if (toolCallIndexes.TryGetValue(blockIndex, out int toolCallIndex))
                                {
                                    yield return new ToolCallSegment
                                    {
                                        Arguments = partialJson ?? "",
                                        Index = toolCallIndex,
                                    };
                                }
                            }
                            else if (deltaType == "signature_delta")
                            {
                                string? signature = delta.TryGetProperty("signature", out JsonElement sig) ? sig.GetString() : null;
                                if (!string.IsNullOrEmpty(signature))
                                {
                                    yield return ChatSegment.FromThinkingSegment(signature);
                                }
                            }
                            // citations_delta - ignore for now
                        }
                        break;
                    }

                case "content_block_stop":
                    {
                        int blockIndex = json.TryGetProperty("index", out JsonElement indexEl) ? indexEl.GetInt32() : -1;
                        if (hostedWebSearchCalls.Remove(blockIndex, out HostedWebSearchCallState? hostedCall))
                        {
                            if (hostedCall.PartialInput.Length > 0)
                            {
                                hostedCall.Block["input"] = JsonNode.Parse(hostedCall.PartialInput.ToString());
                            }

                            string? id = hostedCall.Block["id"]?.GetValue<string>();
                            yield return new ToolCallSegment
                            {
                                Arguments = hostedCall.Block.ToJsonString(JSON.JsonSerializerOptions),
                                Index = hostedCall.SegmentIndex,
                                Id = id,
                                Name = DeepSeekHostedWebSearch.InternalToolName,
                            };
                        }
                        break;
                    }

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

                        JsonElement usageElement = default;
                        bool hasUsage = json.TryGetProperty("usage", out usageElement) && usageElement.ValueKind == JsonValueKind.Object;
                        if (hasUsage)
                        {
                            lastKnownUsage = MergeUsage(lastKnownUsage, usageElement);
                            yield return ChatSegment.FromUsage(lastKnownUsage);
                        }
                        if (finishReason != null)
                        {
                            yield return ChatSegment.FromFinishReason(finishReason);
                        }
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

    private static ChatTokenUsage MergeUsage(ChatTokenUsage? previousUsage, JsonElement usage)
    {
        ChatTokenUsage baseUsage = previousUsage ?? ChatTokenUsage.Zero;
        int cacheTokens = GetUsageValueOrFallback(usage, "cache_read_input_tokens", baseUsage.CacheTokens);
        int freshInputTokens = GetUsageValueOrFallback(usage, "input_tokens", baseUsage.InputFreshTokens);

        return new ChatTokenUsage
        {
            InputTokens = freshInputTokens + cacheTokens,
            OutputTokens = GetUsageValueOrFallback(usage, "output_tokens", baseUsage.OutputTokens),
            CacheTokens = cacheTokens,
            CacheCreationTokens = GetUsageValueOrFallback(usage, "cache_creation_input_tokens", baseUsage.CacheCreationTokens),
            ReasoningTokens = baseUsage.ReasoningTokens,
        };
    }

    private static int GetUsageValueOrFallback(JsonElement usage, string propertyName, int fallback)
    {
        return usage.TryGetProperty(propertyName, out JsonElement valueElement) &&
            valueElement.ValueKind == JsonValueKind.Number &&
            valueElement.TryGetInt32(out int value)
            ? value
            : fallback;
    }

    protected virtual (string url, string apiKey) GetEndpointAndKey(ModelKeySnapshot modelKey)
    {
        string url = (modelKey.Host ?? "https://api.anthropic.com").TrimEnd('/');
        if (url.EndsWith(".ai.azure.com")) // Azure AI Foundry Anthropic
        {
            url += "/anthropic";
        }
        return (url, modelKey.Secret ?? throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "API key is required for Anthropic"));
    }

    protected virtual (string url, string apiKey) GetMessagesEndpointAndKey(ModelSnapshot snapshot)
    {
        string apiKey = snapshot.ModelKeySnapshot.Secret ?? throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "API key is required for Anthropic");

        if (!string.IsNullOrWhiteSpace(snapshot.OverrideUrl))
        {
            return (ModelRequestOverrides.ResolveEndpoint(snapshot), apiKey);
        }

        (string baseUrl, _) = GetEndpointAndKey(snapshot.ModelKeySnapshot);
        return (baseUrl + "/v1/messages", apiKey);
    }

    protected virtual (string url, string apiKey) GetCountTokensEndpointAndKey(ModelSnapshot snapshot)
    {
        (string messagesUrl, string apiKey) = GetMessagesEndpointAndKey(snapshot);
        return (messagesUrl + "/count_tokens", apiKey);
    }

    protected virtual void AddApiKeyHeader(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
    }

    protected virtual JsonNode? BuildThinkingNode(ChatRequest request, bool allowThinking)
    {
        if (allowThinking && request.ChatConfig.ThinkingBudget != null)
        {
            return new JsonObject
            {
                ["type"] = "enabled",
                ["budget_tokens"] = request.ChatConfig.ThinkingBudget.Value
            };
        }
        return null;
    }

    public override async Task<string[]> ListModels(ModelKeySnapshot modelKey, CancellationToken cancellationToken)
    {
        (string url, string apiKey) = GetEndpointAndKey(modelKey);

        using HttpRequestMessage request = new(HttpMethod.Get, url + "/v1/models");
        AddApiKeyHeader(request, apiKey);

        using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceAnthropic);
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
        Model model = request.GetRequiredModel();
        (string url, string apiKey) = GetCountTokensEndpointAndKey(model.CurrentSnapshot);
        JsonObject requestBody = BuildCountTokensRequestBody(request);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, url);
        AddApiKeyHeader(httpRequest, apiKey);
        httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");

        using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceAnthropic);
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

    private JsonObject BuildRequestBody(ChatRequest request)
    {
        Model model = request.GetRequiredModel();
        // Determine thinking block handling
        (bool allowThinkingBlocks, bool allowThinking) = DetermineThinkingSettings(request);

        JsonObject body = new()
        {
            ["max_tokens"] = model.CurrentSnapshot.MaxResponseTokens,
            ["model"] = model.CurrentSnapshot.DeploymentName,
            ["messages"] = ConvertMessages(FilterUnsupportedThinkingBlocks(request.Messages), allowThinkingBlocks, request.Source, SupportsHostedWebSearch),
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

        JsonNode? thinkingNode = BuildThinkingNode(request, allowThinking);
        if (thinkingNode != null)
        {
            body["thinking"] = thinkingNode;
        }

        JsonArray tools = BuildToolsArray(request.Tools);
        if (SupportsHostedWebSearch
            && model.CurrentSnapshot.AllowSearch
            && request.ChatConfig.WebSearchEnabled
            && !tools.Any(DeepSeekHostedWebSearch.IsToolDefinition))
        {
            tools.Add(DeepSeekHostedWebSearch.CreateTool().ToJsonObject());
        }
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
        // Only enforce thinking block rules for WebChat usage, for API/validation we'll leave it flexible
        if (request.Source != UsageSource.WebChat) return (true, true);

        // https://platform.claude.com/docs/zh-CN/build-with-claude/extended-thinking
        // 如果启用了思考，最后的助手转向必须以思考块开始
        IList<NeutralContent> lastAssistantContents = request.Messages
            .LastOrDefault(x => x.Role == NeutralChatRole.Assistant)?.Contents ?? [];

        // Anthropic has strict policies on thinking blocks
        bool hasThinkingBlocks = lastAssistantContents
            .OfType<NeutralThinkContent>()
            .Any();

        bool allThinkingHaveSignature = !hasThinkingBlocks || lastAssistantContents
            .OfType<NeutralThinkContent>()
            .All(tc => tc.Signature != null);

        bool allowThinkingBlocks = hasThinkingBlocks && allThinkingHaveSignature;

        bool hasToolCall = lastAssistantContents.OfType<NeutralToolCallContent>().Any();

        bool allowThinking = !hasToolCall || allowThinkingBlocks;

        return (allowThinkingBlocks, allowThinking);
    }

    private static JsonArray BuildToolsArray(IEnumerable<ChatTool> tools)
    {
        JsonArray result = [];
        foreach (ChatTool tool in tools)
        {
            JsonObject? obj = tool switch
            {
                FunctionTool functionTool => ConvertFunctionTool(functionTool),
                AnthropicBuiltInTool builtInTool => builtInTool.ToJsonObject(),
                _ => null
            };
            if (obj is not null)
            {
                result.Add(obj);
            }
        }
        return result;

        static JsonObject ConvertFunctionTool(FunctionTool tool)
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
    }

    private static JsonNode ParseToolCallInput(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(parameters) ?? new JsonObject();
    }

    private JsonObject BuildCountTokensRequestBody(ChatRequest request)
    {
        Model model = request.GetRequiredModel();
        (bool allowThinkingBlocks, bool allowThinking) = DetermineThinkingSettings(request);

        JsonObject body = new()
        {
            ["model"] = model.CurrentSnapshot.DeploymentName,
            ["messages"] = ConvertMessages(FilterUnsupportedThinkingBlocks(request.Messages), allowThinkingBlocks, request.Source, SupportsHostedWebSearch),
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

        JsonArray tools = BuildToolsArray(request.Tools);
        if (SupportsHostedWebSearch
            && model.CurrentSnapshot.AllowSearch
            && request.ChatConfig.WebSearchEnabled
            && !tools.Any(DeepSeekHostedWebSearch.IsToolDefinition))
        {
            tools.Add(DeepSeekHostedWebSearch.CreateTool().ToJsonObject());
        }
        if (tools.Count > 0)
        {
            body["tools"] = tools;
        }

        return body;
    }

    private static JsonArray ConvertMessages(
        IList<NeutralMessage> messages,
        bool allowThinkingBlocks,
        UsageSource source,
        bool supportsHostedWebSearch)
    {
        List<NeutralMessage> mergedMessages = [.. MergeToolMessages(messages)];
        JsonArray result = [];
        foreach (NeutralMessage msg in mergedMessages)
        {
            // WebChat 的历史 thinking 裁剪已经在 ChatController 按 turn/model 完成；
            // API 入口应尊重用户传入的 thinking/redacted_thinking，不再因为来源是 API 而删除。
            result.Add(ToAnthropicMessage(msg, allowThinkingBlocks, supportsHostedWebSearch));
        }
        return result;

        static IEnumerable<NeutralMessage> MergeToolMessages(IEnumerable<NeutralMessage> messages)
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
                    foreach (NeutralMessage mergedToolMessage in FlushToolBuffer(toolBuffer))
                    {
                        yield return mergedToolMessage;
                    }
                    toolBuffer.Clear();
                    yield return msg;
                }
            }

            foreach (NeutralMessage mergedToolMessage in FlushToolBuffer(toolBuffer))
            {
                yield return mergedToolMessage;
            }

            static IEnumerable<NeutralMessage> FlushToolBuffer(IList<NeutralContent> toolBuffer)
            {
                if (toolBuffer.Count == 0)
                {
                    yield break;
                }

                yield return new NeutralMessage
                {
                    Role = NeutralChatRole.User,
                    Contents = [.. toolBuffer],
                };
            }
        }

        static JsonObject ToAnthropicMessage(
            NeutralMessage message,
            bool allowThinkingBlocks,
            bool supportsHostedWebSearch)
        {
            string anthropicRole = message.Role switch
            {
                NeutralChatRole.User => "user",
                NeutralChatRole.Assistant => "assistant",
                NeutralChatRole.System => "system",
                NeutralChatRole.Tool => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "Tool messages should be merged into user messages before conversion."),
                _ => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, $"Unknown message role: {message.Role}"),
            };

            JsonArray content = [];
            IReadOnlyList<NeutralToolResponseGroup> toolResponseGroups = anthropicRole == "user"
                ? message.GetToolResponseGroups()
                : [];

            if (toolResponseGroups.Count > 0)
            {
                foreach (NeutralToolResponseGroup group in toolResponseGroups)
                {
                    content.Add(CreateToolResultMessageBlock(group));
                }
            }
            else
            {
                foreach (NeutralContent c in message.Contents)
                {
                    JsonObject? contentBlock = ToAnthropicContent(c, allowThinkingBlocks, supportsHostedWebSearch);
                    if (contentBlock != null)
                    {
                        content.Add(contentBlock);
                    }
                }
            }

            return new JsonObject
            {
                ["role"] = anthropicRole,
                ["content"] = content
            };

            static JsonObject CreateToolResultMessageBlock(NeutralToolResponseGroup group)
            {
                JsonObject result = new()
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = group.ToolResponse.ToolCallId,
                    ["content"] = BuildToolResultMessageContent(group)
                };
                if (!group.ToolResponse.IsSuccess)
                {
                    result["is_error"] = true;
                }
                return result;
            }

            static JsonNode BuildToolResultMessageContent(NeutralToolResponseGroup group)
            {
                if (group.AttachedContents.Count == 0)
                {
                    return group.ToolResponse.Response;
                }

                JsonArray blocks = [];
                if (!string.IsNullOrEmpty(group.ToolResponse.Response))
                {
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = group.ToolResponse.Response
                    });
                }

                foreach (NeutralContent attachedContent in group.AttachedContents)
                {
                    JsonObject? block = ToAnthropicToolResultPart(attachedContent);
                    if (block != null)
                    {
                        blocks.Add(block);
                    }
                }

                return blocks.Count > 0 ? blocks : group.ToolResponse.Response;
            }

            static JsonObject? ToAnthropicToolResultPart(NeutralContent content)
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
                    NeutralFileContent => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "FileId should be converted to FileUrl/FileBlob before conversion."),
                    _ => null
                };

                if (result != null && content.CacheControl != null)
                {
                    result["cache_control"] = new JsonObject { ["type"] = content.CacheControl.Type };
                }

                return result;
            }

            static JsonObject? ToAnthropicContent(
                NeutralContent content,
                bool allowThinkingBlocks,
                bool supportsHostedWebSearch)
            {
                JsonObject? result;
                if (supportsHostedWebSearch
                    && content is NeutralToolCallContent { Name: DeepSeekHostedWebSearch.InternalToolName } hostedCall
                    && DeepSeekHostedWebSearch.TryParseBlock(hostedCall.Parameters, DeepSeekHostedWebSearch.ServerToolUseType, out JsonObject? serverToolUse))
                {
                    result = serverToolUse;
                }
                else if (supportsHostedWebSearch
                    && content is NeutralToolCallResponseContent hostedResponse
                    && DeepSeekHostedWebSearch.TryParseBlock(hostedResponse.Response, DeepSeekHostedWebSearch.ToolResultType, out JsonObject? toolResult))
                {
                    result = toolResult;
                }
                else
                {
                    result = content switch
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
                            ["input"] = ParseToolCallInput(toolCall.Parameters)
                        },
                        NeutralToolCallResponseContent toolResp => CreateToolResultBlock(new NeutralToolResponseGroup
                        {
                            ToolResponse = toolResp,
                            AttachedContents = []
                        }),
                        NeutralFileContent => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "FileId should be converted to FileUrl/FileBlob before conversion."),
                        _ => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, $"Unsupported content type: {content.GetType().Name}")
                    };
                }

                // Add cache control if present
                if (result != null && content.CacheControl != null)
                {
                    result["cache_control"] = new JsonObject { ["type"] = content.CacheControl.Type };
                }

                return result;

                static JsonObject CreateToolResultBlock(NeutralToolResponseGroup group)
                {
                    return CreateToolResultMessageBlock(group);
                }

                static JsonObject CreateThinkingBlock(NeutralThinkContent think)
                {
                    if (string.IsNullOrEmpty(think.Content))
                    {
                        return new JsonObject
                        {
                            ["type"] = "redacted_thinking",
                            ["data"] = think.Signature
                        };
                    }
                    else
                    {
                        return new JsonObject
                        {
                            ["type"] = "thinking",
                            ["thinking"] = think.Content,
                            ["signature"] = think.Signature,
                        };
                    }
                }
            }
        }
    }

    private IList<NeutralMessage> FilterUnsupportedThinkingBlocks(IList<NeutralMessage> messages)
    {
        if (SupportsRedactedThinking)
        {
            return messages;
        }

        return [.. messages
            .Select(message => message with
            {
                Contents = [.. message.Contents.Where(content =>
                    content is not NeutralThinkContent think || !string.IsNullOrEmpty(think.Content))]
            })
            .Where(message => message.Contents.Count > 0)];
    }
}
