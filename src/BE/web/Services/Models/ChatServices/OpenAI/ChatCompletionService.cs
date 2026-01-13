using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public partial class ChatCompletionService(IHttpClientFactory httpClientFactory) : ChatService
{
    protected override HashSet<string> SupportedContentTypes =>
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    ];

    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        string url = GetEndpoint(modelKey);
        using HttpRequestMessage request = new(HttpMethod.Get, url + "/models");
        AddAuthorizationHeader(request, modelKey);

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

    protected virtual string GetEndpoint(ModelKey modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DBModelProvider)modelKey.ModelProviderId);
        }
        return host?.TrimEnd('/') ?? "";
    }

    protected virtual void AddAuthorizationHeader(HttpRequestMessage request, ModelKey modelKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", modelKey.Secret);
    }

    protected virtual string ReasoningContentPropertyName => "reasoning_content";

    /// <summary>
    /// Controls how to persist thinking/reasoning content returned from upstream.
    /// Default behavior keeps existing functionality: persist it as <see cref="ThinkChatSegment"/>.
    /// Override to drop or transform the thinking content.
    /// </summary>
    protected virtual bool TryCreateThinkingSegmentForStorage(string thinkingContent, out ChatSegment? segment)
    {
        if (string.IsNullOrEmpty(thinkingContent))
        {
            segment = null;
            return false;
        }

        segment = ChatSegment.FromThink(thinkingContent);
        return true;
    }

    /// <summary>
    /// Controls how to persist thinking/reasoning content returned from upstream, with an optional signature.
    /// Some providers (e.g., MiniMax `reasoning_details`) return structured reasoning payloads that should be
    /// preserved in history for interleaved thinking.
    /// Default behavior ignores the signature and uses <see cref="TryCreateThinkingSegmentForStorage(string, out ChatSegment?)"/>.
    /// </summary>
    protected virtual bool TryCreateThinkingSegmentForStorage(string thinkingContent, string? thinkingSignature, out ChatSegment? segment)
    {
        return TryCreateThinkingSegmentForStorage(thinkingContent, out segment);
    }

    /// <summary>
    /// Controls how to send thinking/reasoning content back to upstream in the next request.
    /// Default behavior sends nothing to avoid impacting providers that don't support it.
    /// Override in provider-specific services (e.g., DeepSeek) to support interleaved thinking.
    /// </summary>
    protected virtual bool TryBuildThinkingContentForRequest(
        NeutralMessage message,
        IReadOnlyList<NeutralThinkContent> thinkingContents,
        IReadOnlyList<NeutralToolCallContent> toolCalls,
        out string? thinkingContent)
    {
        thinkingContent = null;
        return false;
    }

    /// <summary>
    /// Controls how to send thinking/reasoning content back to upstream in the next request.
    /// Use this when the upstream expects a structured reasoning payload (e.g., MiniMax `reasoning_details`).
    /// Default behavior returns false.
    /// </summary>
    protected virtual bool TryBuildThinkingNodeForRequest(
        NeutralMessage message,
        IReadOnlyList<NeutralThinkContent> thinkingContents,
        IReadOnlyList<NeutralToolCallContent> toolCalls,
        out JsonNode? thinkingNode)
    {
        thinkingNode = null;
        return false;
    }

    private static string? ExtractThinkingText(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Object => el.TryGetProperty("text", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
            JsonValueKind.Array => ExtractThinkingTextFromArray(el),
            _ => null
        };
    }

    private static string? ExtractThinkingTextFromArray(JsonElement el)
    {
        StringBuilder sb = new();
        foreach (JsonElement item in el.EnumerateArray())
        {
            string? part = ExtractThinkingText(item);
            if (!string.IsNullOrEmpty(part))
            {
                sb.Append(part);
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private (string? text, string? signature) TryGetThinkingPayload(JsonElement obj)
    {
        if (!obj.TryGetProperty(ReasoningContentPropertyName, out JsonElement rc))
        {
            return (null, null);
        }

        string? text = ExtractThinkingText(rc);
        string? signature = rc.ValueKind is JsonValueKind.Array or JsonValueKind.Object ? rc.GetRawText() : null;
        return (text, signature);
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!request.ChatConfig.Model.AllowStreaming || !request.Streamed)
        {
            await foreach (ChatSegment segment in ChatNonStreaming(request, cancellationToken))
            {
                yield return segment;
            }
            yield break;
        }

        string url = GetEndpoint(request.ChatConfig.Model.ModelKey);
        JsonObject requestBody = BuildRequestBody(request, stream: true);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, url + "/chat/completions");
        AddAuthorizationHeader(httpRequest, request.ChatConfig.Model.ModelKey);
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

        DBFinishReason? finishReason = null;

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

            // Parse choices
            if (!json.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0)
            {
                if (json.TryGetProperty("usage", out JsonElement usageOnly))
                {
                    ChatTokenUsage? usageSegment = ParseUsage(usageOnly);
                    if (usageSegment != null)
                    {
                        yield return new UsageChatSegment
                        {
                            Usage = usageSegment,
                        };
                    }
                }
                continue;
            }

            JsonElement choice = choices[0];
            JsonElement delta = choice.TryGetProperty("delta", out JsonElement d) ? d : default;

            // Parse content
            string? content = delta.TryGetProperty("content", out JsonElement c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

            // Parse reasoning content (for models like DeepSeek-R1)
            (string? reasoningContent, string? reasoningSignature) = TryGetThinkingPayload(delta);

            // Parse tool calls
            List<ToolCallSegment> toolCallSegments = [];
            if (delta.TryGetProperty("tool_calls", out JsonElement toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement tc in toolCalls.EnumerateArray())
                {
                    int index = tc.TryGetProperty("index", out JsonElement idx) ? idx.GetInt32() : 0;
                    string? id = tc.TryGetProperty("id", out JsonElement tcId) ? tcId.GetString() : null;
                    string? functionName = null;
                    string? functionArgs = null;

                    if (tc.TryGetProperty("function", out JsonElement func))
                    {
                        functionName = func.TryGetProperty("name", out JsonElement fn) ? fn.GetString() : null;
                        functionArgs = func.TryGetProperty("arguments", out JsonElement fa) ? fa.GetString() : null;
                    }

                    toolCallSegments.Add(new ToolCallSegment
                    {
                        Index = index,
                        Id = id,
                        Name = functionName,
                        Arguments = functionArgs ?? ""
                    });
                }
            }

            // Parse finish reason
            if (choice.TryGetProperty("finish_reason", out JsonElement fr) && fr.ValueKind == JsonValueKind.String)
            {
                finishReason = DBFinishReasonParser.Parse(fr.GetString());
            }

            // Parse usage
            ChatTokenUsage? usage = null;
            if (json.TryGetProperty("usage", out JsonElement u))
            {
                usage = ParseUsage(u);
            }

            // Skip empty updates
            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(reasoningContent) && toolCallSegments.Count == 0 && usage == null && finishReason == null)
            {
                continue;
            }

            List<ChatSegment> items = [];
            if (!string.IsNullOrEmpty(content))
            {
                items.Add(ChatSegment.FromText(content));
            }

            if (!string.IsNullOrEmpty(reasoningContent) && TryCreateThinkingSegmentForStorage(reasoningContent, reasoningSignature, out ChatSegment? thinkSegment) && thinkSegment != null)
            {
                items.Add(thinkSegment);
            }
            items.AddRange(toolCallSegments);

            foreach (ChatSegment item in items)
            {
                yield return item;
            }

            if (usage != null)
            {
                yield return new UsageChatSegment
                {
                    Usage = usage,
                };
            }

            if (finishReason != null)
            {
                yield return new FinishReasonChatSegment
                {
                    FinishReason = finishReason,
                };
                finishReason = null;
            }
        }
    }

    private static int GetReasoningTokens(JsonElement usage)
    {
        if (usage.TryGetProperty("completion_tokens_details", out JsonElement ctd) &&
            ctd.TryGetProperty("reasoning_tokens", out JsonElement rt))
        {
            return rt.GetInt32();
        }
        return 0;
    }

    protected virtual int GetCachedTokens(JsonElement usage)
    {
        // OpenAI style: usage.prompt_tokens_details.cached_tokens
        if (usage.TryGetProperty("prompt_tokens_details", out JsonElement ptd) && ptd.ValueKind == JsonValueKind.Object &&
            ptd.TryGetProperty("cached_tokens", out JsonElement cached) && cached.ValueKind == JsonValueKind.Number)
        {
            return cached.GetInt32();
        }

        return 0;
    }

    private ChatTokenUsage? ParseUsage(JsonElement usageElement)
    {
        if (usageElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ChatTokenUsage
        {
            InputTokens = usageElement.TryGetProperty("prompt_tokens", out JsonElement pt) ? pt.GetInt32() : 0,
            OutputTokens = usageElement.TryGetProperty("completion_tokens", out JsonElement ct) ? ct.GetInt32() : 0,
            ReasoningTokens = GetReasoningTokens(usageElement),
            CacheTokens = GetCachedTokens(usageElement)
        };
    }


    private async IAsyncEnumerable<ChatSegment> ChatNonStreaming(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string url = GetEndpoint(request.ChatConfig.Model.ModelKey);
        JsonObject requestBody = BuildRequestBody(request, stream: false);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, url + "/chat/completions");
        AddAuthorizationHeader(httpRequest, request.ChatConfig.Model.ModelKey);
        httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");

        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = NetworkTimeout;
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RawChatServiceException((int)response.StatusCode, errorBody);
        }

        string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument doc = JsonDocument.Parse(jsonResponse);
        JsonElement root = doc.RootElement;

        List<ChatSegment> items = [];
        DBFinishReason? finishReason = null;
        ChatTokenUsage? usage = null;

        if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
        {
            JsonElement choice = choices[0];
            if (choice.TryGetProperty("message", out JsonElement message))
            {
                // Content
                if (message.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.String)
                {
                    string? text = content.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        items.Add(ChatSegment.FromText(text));
                    }
                }

                // Reasoning content
                (string? reasoning, string? reasoningSignature) = TryGetThinkingPayload(message);
                if (!string.IsNullOrEmpty(reasoning) && TryCreateThinkingSegmentForStorage(reasoning, reasoningSignature, out ChatSegment? thinkSegment) && thinkSegment != null)
                {
                    items.Add(thinkSegment);
                }

                // Tool calls
                if (message.TryGetProperty("tool_calls", out JsonElement toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (JsonElement tc in toolCalls.EnumerateArray())
                    {
                        string? id = tc.TryGetProperty("id", out JsonElement tcId) ? tcId.GetString() : null;
                        string? functionName = null;
                        string? functionArgs = null;

                        if (tc.TryGetProperty("function", out JsonElement func))
                        {
                            functionName = func.TryGetProperty("name", out JsonElement fn) ? fn.GetString() : null;
                            functionArgs = func.TryGetProperty("arguments", out JsonElement fa) ? fa.GetString() : null;
                        }

                        items.Add(new ToolCallSegment
                        {
                            Index = index++,
                            Id = id,
                            Name = functionName,
                            Arguments = functionArgs ?? ""
                        });
                    }
                }
            }

            // Finish reason
            if (choice.TryGetProperty("finish_reason", out JsonElement fr) && fr.ValueKind == JsonValueKind.String)
            {
                finishReason = DBFinishReasonParser.Parse(fr.GetString());
            }
        }

        // Usage
        if (root.TryGetProperty("usage", out JsonElement u))
        {
            usage = new ChatTokenUsage
            {
                InputTokens = u.TryGetProperty("prompt_tokens", out JsonElement pit) ? pit.GetInt32() : 0,
                OutputTokens = u.TryGetProperty("completion_tokens", out JsonElement cot) ? cot.GetInt32() : 0,
                ReasoningTokens = GetReasoningTokens(u),
                CacheTokens = GetCachedTokens(u)
            };
        }

        foreach (ChatSegment item in items)
        {
            yield return item;
        }

        if (usage != null)
        {
            yield return new UsageChatSegment
            {
                Usage = usage
            };
        }

        yield return new FinishReasonChatSegment
        {
            FinishReason = finishReason ?? DBFinishReason.Success
        };
    }

    protected virtual JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = new()
        {
            ["model"] = request.ChatConfig.Model.DeploymentName,
            ["messages"] = BuildMessages(request),
            ["stream"] = stream,
        };

        if (stream)
        {
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        if (request.ChatConfig.Temperature.HasValue)
        {
            body["temperature"] = request.ChatConfig.Temperature.Value;
        }

        if (request.ChatConfig.MaxOutputTokens.HasValue)
        {
            if (request.ChatConfig.Model.UseMaxCompletionTokens)
            {
                body["max_completion_tokens"] = request.ChatConfig.MaxOutputTokens.Value;
            }
            else
            {
                body["max_tokens"] = request.ChatConfig.MaxOutputTokens.Value;
            }
        }

        if (request.EndUserId != null)
        {
            body["user"] = request.EndUserId;
        }

        if (request.TopP.HasValue)
        {
            body["top_p"] = request.TopP.Value;
        }

        if (request.Seed.HasValue)
        {
            body["seed"] = request.Seed.Value;
        }

        if (request.Source == UsageSource.WebChat && request.Tools.Any())
        {
            body["parallel_tool_calls"] = true;
        }
        else if (request.AllowParallelToolCalls.HasValue)
        {
            body["parallel_tool_calls"] = request.AllowParallelToolCalls.Value;
        }

        if (request.TextFormat != null)
        {
            JsonNode? responseFormat = request.TextFormat.ToJsonNode();
            if (responseFormat != null)
            {
                body["response_format"] = responseFormat;
            }
        }

        // Tools
        JsonArray toolsArray = BuildFunctionToolsArray(request.Tools);
        if (toolsArray.Count > 0)
        {
            body["tools"] = toolsArray;
        }

        return body;
    }

    private static JsonArray BuildFunctionToolsArray(IEnumerable<ChatTool> tools)
    {
        JsonArray result = [];
        foreach (ChatTool tool in tools)
        {
            if (tool is FunctionTool functionTool)
            {
                result.Add(functionTool.ToChatCompletionToolCall());
            }
        }
        return result;
    }

    protected virtual JsonArray BuildMessages(ChatRequest request)
    {
        JsonArray messages = [];

        // System prompt
        string? systemPrompt = request.GetEffectiveSystemPrompt();
        if (systemPrompt != null)
        {
            messages.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = systemPrompt
            });
        }

        // User/Assistant messages
        foreach (NeutralMessage msg in request.Messages)
        {
            messages.Add(ToOpenAIMessage(msg));
        }

        return messages;
    }

    protected virtual JsonObject ToOpenAIMessage(NeutralMessage message)
    {
        string role = message.Role switch
        {
            NeutralChatRole.User => "user",
            NeutralChatRole.Assistant => "assistant",
            NeutralChatRole.Tool => "tool",
            _ => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, $"Role {message.Role} is not supported")
        };

        if (message.Role == NeutralChatRole.Tool)
        {
            NeutralToolCallResponseContent toolResp = (NeutralToolCallResponseContent)message.Contents.First();
            return new JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = toolResp.ToolCallId,
                ["content"] = toolResp.Response
            };
        }

        // Check if we have tool calls (assistant message)
        List<NeutralToolCallContent> toolCalls = message.Contents.OfType<NeutralToolCallContent>().ToList();
        List<NeutralThinkContent> thinkingContents = message.Contents.OfType<NeutralThinkContent>().ToList();
        List<NeutralContent> otherContents = message.Contents.Where(c => c is not NeutralToolCallContent and not NeutralThinkContent).ToList();

        JsonObject msg = new() { ["role"] = role };

        // Build content
        if (otherContents.Count == 1 && otherContents[0] is NeutralTextContent textContent)
        {
            // Simple text content
            msg["content"] = textContent.Content;
        }
        else if (otherContents.Count > 0)
        {
            // Multi-part content
            JsonArray contentArray = [];
            foreach (NeutralContent content in otherContents)
            {
                JsonObject? part = ToOpenAIContentPart(content);
                if (part != null)
                {
                    contentArray.Add(part);
                }
            }
            if (contentArray.Count > 0)
            {
                msg["content"] = contentArray;
            }
        }

        // Tool calls for assistant messages
        if (toolCalls.Count > 0 && role == "assistant")
        {
            JsonArray toolCallsArray = [];
            foreach (NeutralToolCallContent tc in toolCalls)
            {
                toolCallsArray.Add(new JsonObject
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Parameters
                    }
                });
            }
            msg["tool_calls"] = toolCallsArray;
        }

        if (TryBuildThinkingNodeForRequest(message, thinkingContents, toolCalls, out JsonNode? thinkingNode) && thinkingNode != null)
        {
            msg[ReasoningContentPropertyName] = thinkingNode;
        }
        else if (TryBuildThinkingContentForRequest(message, thinkingContents, toolCalls, out string? thinkingContent) && !string.IsNullOrEmpty(thinkingContent))
        {
            msg[ReasoningContentPropertyName] = thinkingContent;
        }

        return msg;
    }

    protected virtual JsonObject? ToOpenAIContentPart(NeutralContent content)
    {
        return content switch
        {
            NeutralTextContent text => new JsonObject
            {
                ["type"] = "text",
                ["text"] = text.Content
            },
            NeutralErrorContent error => new JsonObject
            {
                ["type"] = "text",
                ["text"] = error.Content
            },
            NeutralFileUrlContent fileUrl => new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject { ["url"] = fileUrl.Url }
            },
            NeutralFileBlobContent fileBlob => new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject
                {
                    ["url"] = $"data:{fileBlob.MediaType};base64,{Convert.ToBase64String(fileBlob.Data)}"
                }
            },
            NeutralThinkContent => null, // ChatCompletion API does not support "think" content type
            NeutralToolCallContent => null, // Tool calls are handled separately
            NeutralToolCallResponseContent => null, // Tool responses are handled separately
            NeutralFileContent => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "FileId content type should be converted to FileUrl or FileBlob before sending to OpenAI API in PreProcess."),
            _ => throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, $"Content type {content.GetType().Name} is not supported.")
        };
    }
}
