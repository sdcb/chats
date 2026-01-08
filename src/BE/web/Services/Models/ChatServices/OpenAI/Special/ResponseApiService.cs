using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChatTokenUsage = Chats.BE.Services.Models.Dtos.ChatTokenUsage;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Special;

public class ResponseApiService(IHttpClientFactory httpClientFactory, ILogger<ResponseApiService> logger) : ChatService
{
    protected override HashSet<string> SupportedContentTypes =>
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    ];

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

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string endpoint = GetEndpoint(request.ChatConfig.Model.ModelKey);
        bool hasTools = false;

        if (request.ChatConfig.Model.UseAsyncApi)
        {
            // Background mode
            Stopwatch sw = Stopwatch.StartNew();
            JsonObject requestBody = BuildRequestBody(request, stream: false, background: true);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/responses");
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

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonObject? responseObj = JsonSerializer.Deserialize<JsonObject>(responseJson);
            string? responseId = responseObj?["id"]?.GetValue<string>();
            string? status = responseObj?["status"]?.GetValue<string>();

            if (responseId == null)
            {
                throw new CustomChatServiceException(DBFinishReason.UpstreamError, "Response ID not found in the response.");
            }

            CancellationTokenRegistration? cancelRegistration = cancellationToken.Register(async () =>
            {
                if (status == "in_progress" || status == "queued")
                {
                    try
                    {
                        using HttpRequestMessage cancelRequest = new(HttpMethod.Post, $"{endpoint}/v1/responses/{responseId}/cancel");
                        AddAuthorizationHeader(cancelRequest, request.ChatConfig.Model.ModelKey);
                        using HttpClient cancelClient = httpClientFactory.CreateClient();
                        await cancelClient.SendAsync(cancelRequest, default);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error cancelling response {responseId}: {ex.Message}", responseId, ex.Message);
                    }
                }
            });

            bool cancelled = false;
            try
            {
                while (status == "in_progress" || status == "queued")
                {
                    logger.LogInformation("{responseId} status: {status}, elapsed: {sw.ElapsedMilliseconds:N0}ms", responseId, status, sw.ElapsedMilliseconds);
                    await Task.Delay(2000, cancellationToken);

                    using HttpRequestMessage getRequest = new(HttpMethod.Get, $"{endpoint}/v1/responses/{responseId}");
                    AddAuthorizationHeader(getRequest, request.ChatConfig.Model.ModelKey);

                    using HttpResponseMessage getResponse = await httpClient.SendAsync(getRequest, cancellationToken);
                    responseJson = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                    responseObj = JsonSerializer.Deserialize<JsonObject>(responseJson);
                    status = responseObj?["status"]?.GetValue<string>();
                }
            }
            catch (TaskCanceledException)
            {
                cancelled = true;
            }

            cancelRegistration?.Dispose();
            logger.LogInformation("Response {responseId} completed with status: {status}, elapsed={sw.ElapsedMilliseconds:N0}ms", responseId, status, sw.ElapsedMilliseconds);

            ChatTokenUsage? usage = ParseUsage(responseObj?["usage"]);

            if (status == "incomplete")
            {
                if (usage != null)
                {
                    yield return ChatSegment.FromUsage(usage);
                }
                yield return ChatSegment.FromFinishReason(DBFinishReason.Length);
            }
            else if (status == "failed")
            {
                string? errorMessage = responseObj?["error"]?.ToString() ?? "Response failed";
                if (usage != null)
                {
                    yield return ChatSegment.FromUsage(usage);
                }
                yield return ChatSegment.FromFinishReason(DBFinishReason.ContentFilter);
                throw new CustomChatServiceException(DBFinishReason.ContentFilter, errorMessage);
            }
            else if (status == "cancelled" || cancelled)
            {
                if (usage != null)
                {
                    yield return ChatSegment.FromUsage(usage);
                }
                throw new TaskCanceledException();
            }
            else if (status != "completed")
            {
                throw new CustomChatServiceException(DBFinishReason.UpstreamError, $"Unsupported response status: {status}");
            }
            else
            {
                // Completed - parse output items
                int fcIndex = 0;
                JsonArray? outputItems = responseObj?["output"]?.AsArray();
                if (outputItems != null)
                {
                    foreach (JsonNode? item in outputItems)
                    {
                        string? itemType = item?["type"]?.GetValue<string>();

                        if (itemType == "reasoning")
                        {
                            JsonArray? summaryArray = item?["summary"]?.AsArray();
                            if (summaryArray != null)
                            {
                                StringBuilder thinkText = new();
                                foreach (JsonNode? summaryPart in summaryArray)
                                {
                                    string? partType = summaryPart?["type"]?.GetValue<string>();
                                    if (partType == "summary_text")
                                    {
                                        string? text = summaryPart?["text"]?.GetValue<string>();
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            if (thinkText.Length > 0) thinkText.Append("\n\n");
                                            thinkText.Append(text);
                                        }
                                    }
                                }
                                if (thinkText.Length > 0)
                                {
                                    yield return ChatSegment.FromThink(thinkText.ToString());
                                }
                            }
                        }
                        else if (itemType == "function_call")
                        {
                            hasTools = true;
                            string? callId = item?["call_id"]?.GetValue<string>();
                            string? name = item?["name"]?.GetValue<string>();
                            string? arguments = item?["arguments"]?.GetValue<string>();
                            yield return new ToolCallSegment
                            {
                                Index = fcIndex++,
                                Id = callId,
                                Name = name,
                                Arguments = arguments ?? "",
                            };
                        }
                        else if (itemType == "message")
                        {
                            JsonArray? contentArray = item?["content"]?.AsArray();
                            if (contentArray != null)
                            {
                                foreach (JsonNode? contentPart in contentArray)
                                {
                                    string? kind = contentPart?["type"]?.GetValue<string>();
                                    if (kind == "output_text")
                                    {
                                        string? text = contentPart?["text"]?.GetValue<string>();
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            yield return ChatSegment.FromText(text);
                                        }
                                    }
                                    else if (kind == "refusal")
                                    {
                                        string? refusal = contentPart?["refusal"]?.GetValue<string>();
                                        throw new CustomChatServiceException(DBFinishReason.ContentFilter, refusal ?? "Refusal");
                                    }
                                }
                            }
                        }
                    }
                }

                if (usage != null)
                {
                    yield return ChatSegment.FromUsage(usage);
                }
                yield return ChatSegment.FromFinishReason(hasTools ? DBFinishReason.ToolCalls : DBFinishReason.Success);
            }
        }
        else
        {
            // Streaming mode
            JsonObject requestBody = BuildRequestBody(request, stream: true, background: false);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/responses");
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

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            int fcIndex = 0;
            string? currentFcId = null;
            string? currentFcName = null;

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

                string? eventType = json.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : null;
                if (eventType == null) continue;

                if (eventType == "error")
                {
                    string? errorMessage = json.TryGetProperty("error", out JsonElement errorEl) ? errorEl.ToString() : "Unknown error";
                    throw new CustomChatServiceException(DBFinishReason.UpstreamError, errorMessage ?? "Unknown error");
                }
                else if (eventType == "response.output_text.delta")
                {
                    string? delta = json.TryGetProperty("delta", out JsonElement deltaEl) ? deltaEl.GetString() : null;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return ChatSegment.FromText(delta);
                    }
                }
                else if (eventType == "response.completed")
                {
                    JsonElement? responseEl = json.TryGetProperty("response", out JsonElement respEl) ? respEl : null;
                    ChatTokenUsage? usage = ParseUsage(responseEl?.GetProperty("usage"));
                    string? status = responseEl?.GetProperty("status").GetString();

                    DBFinishReason? finishReason = status switch
                    {
                        null => null,
                        "failed" => DBFinishReason.ContentFilter,
                        "completed" => hasTools ? DBFinishReason.ToolCalls : DBFinishReason.Success,
                        "incomplete" => DBFinishReason.Length,
                        _ => null,
                    };

                    if (usage != null)
                    {
                        yield return ChatSegment.FromUsage(usage);
                    }
                    if (finishReason != null)
                    {
                        yield return ChatSegment.FromFinishReason(finishReason);
                    }
                }
                else if (eventType == "response.output_item.added")
                {
                    if (json.TryGetProperty("item", out JsonElement itemEl) && itemEl.TryGetProperty("type", out JsonElement itemTypeEl) && itemTypeEl.GetString() == "function_call")
                    {
                        hasTools = true;
                        currentFcId = itemEl.TryGetProperty("call_id", out JsonElement callIdEl) ? callIdEl.GetString() : null;
                        currentFcName = itemEl.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                        yield return new ToolCallSegment
                        {
                            Index = fcIndex,
                            Id = currentFcId,
                            Name = currentFcName,
                            Arguments = "",
                        };
                    }
                }
                else if (eventType == "response.function_call_arguments.delta")
                {
                    string? delta = json.TryGetProperty("delta", out JsonElement deltaEl) ? deltaEl.GetString() : null;
                    yield return new ToolCallSegment
                    {
                        Index = fcIndex,
                        Arguments = delta ?? "",
                    };
                }
                else if (eventType == "response.output_item.done")
                {
                    if (json.TryGetProperty("item", out JsonElement itemEl) && itemEl.TryGetProperty("type", out JsonElement itemTypeEl))
                    {
                        string? itemType = itemTypeEl.GetString();
                        if (itemType == "function_call")
                        {
                            fcIndex++;
                        }
                        else if (itemType == "reasoning")
                        {
                            // When include contains reasoning.encrypted_content, the server can return an encrypted signature for thinking.
                            // We store it in ThinkChatSegment.Signature for later use.
                            if (itemEl.TryGetProperty("encrypted_content", out JsonElement encryptedEl))
                            {
                                string? encrypted = encryptedEl.GetString();
                                if (!string.IsNullOrEmpty(encrypted))
                                {
                                    yield return ChatSegment.FromThinkingSegment(encrypted);
                                }
                            }
                        }
                    }
                }
                else if (eventType == "response.reasoning_summary_text.delta")
                {
                    string? delta = json.TryGetProperty("delta", out JsonElement deltaEl) ? deltaEl.GetString() : null;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return ChatSegment.FromThink(delta);
                    }
                }
                else if (eventType == "response.reasoning_summary_text.done")
                {
                    yield return ChatSegment.FromThink("\n\n");
                }
            }
        }
    }

    private static ChatTokenUsage? ParseUsage(JsonElement? usageEl)
    {
        if (usageEl == null || usageEl.Value.ValueKind != JsonValueKind.Object) return null;
        JsonElement usage = usageEl.Value;
        int inputTokens = usage.TryGetProperty("input_tokens", out JsonElement inputEl) ? inputEl.GetInt32() : 0;
        int outputTokens = usage.TryGetProperty("output_tokens", out JsonElement outputEl) ? outputEl.GetInt32() : 0;
        int reasoningTokens = 0;
        if (usage.TryGetProperty("output_tokens_details", out JsonElement detailsEl) && detailsEl.TryGetProperty("reasoning_tokens", out JsonElement reasoningEl))
        {
            reasoningTokens = reasoningEl.GetInt32();
        }
        return new ChatTokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            CacheTokens = GetCachedTokens(usage),
        };
    }

    private static ChatTokenUsage? ParseUsage(JsonNode? usageNode)
    {
        if (usageNode == null) return null;
        int inputTokens = usageNode["input_tokens"]?.GetValue<int>() ?? 0;
        int outputTokens = usageNode["output_tokens"]?.GetValue<int>() ?? 0;
        int reasoningTokens = usageNode["output_tokens_details"]?["reasoning_tokens"]?.GetValue<int>() ?? 0;
        return new ChatTokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            CacheTokens = usageNode["input_tokens_details"]?["cached_tokens"]?.GetValue<int>() ?? 0,
        };
    }

    private static int GetCachedTokens(JsonElement usage)
    {
        if (usage.TryGetProperty("input_tokens_details", out JsonElement inputDetails) &&
            inputDetails.TryGetProperty("cached_tokens", out JsonElement cachedInput))
        {
            return cachedInput.GetInt32();
        }
        return 0;
    }

    private JsonObject BuildRequestBody(ChatRequest request, bool stream, bool background)
    {
        JsonObject body = new()
        {
            ["model"] = request.ChatConfig.Model.DeploymentName,
            ["input"] = BuildInputArray(request),
            ["stream"] = stream,
        };

        if (request.ChatConfig.Temperature != null)
        {
            body["temperature"] = request.ChatConfig.Temperature.Value;
        }

        if (request.EndUserId != null)
        {
            body["user"] = request.EndUserId;
        }

        if (request.ChatConfig.MaxOutputTokens != null)
        {
            body["max_output_tokens"] = request.ChatConfig.MaxOutputTokens.Value;
        }

        // Reasoning options - only add if explicitly specified
        string? reasoningEffort = request.ChatConfig.ReasoningEffort.ToReasoningEffortString();
        if (reasoningEffort != null)
        {
            body["reasoning"] = new JsonObject
            {
                ["effort"] = reasoningEffort,
                ["summary"] = "detailed",
            };

            // Request encrypted thinking signature.
            body["include"] = new JsonArray { "reasoning.encrypted_content" };
        }

        // Text format
        if (request.TextFormat != null)
        {
            body["text"] = new JsonObject
            {
                ["format"] = request.TextFormat.ToJsonObject()
            };
        }

        // Tools
        JsonArray functionTools = [];
        foreach (FunctionTool tool in request.Tools.OfType<FunctionTool>())
        {
            functionTools.Add(tool.ToResponseToolCall());
        }
        if (functionTools.Count > 0)
        {
            body["tools"] = functionTools;
        }

        if (background)
        {
            body["background"] = true;
        }

        return body;
    }

    private static JsonArray BuildInputArray(ChatRequest request)
    {
        JsonArray input = [];

        string? effectiveSystemPrompt = request.GetEffectiveSystemPrompt();
        if (effectiveSystemPrompt != null)
        {
            input.Add(new JsonObject
            {
                ["type"] = "message",
                ["role"] = "system",
                ["content"] = new JsonArray { new JsonObject { ["type"] = "input_text", ["text"] = effectiveSystemPrompt } }
            });
        }

        foreach (NeutralMessage message in request.Messages)
        {
            if (message.Role == NeutralChatRole.User)
            {
                input.Add(new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = ContentToInputParts(message.Contents)
                });
            }
            else if (message.Role == NeutralChatRole.Assistant)
            {
                // Preserve thinking signature for multi-step tool calls (Response API).
                foreach (NeutralThinkContent think in message.Contents.OfType<NeutralThinkContent>())
                {
                    if (!string.IsNullOrWhiteSpace(think.Signature))
                    {
                        input.Add(new JsonObject
                        {
                            ["type"] = "reasoning",
                            ["encrypted_content"] = think.Signature,
                            ["summary"] = new JsonArray(),
                        });
                    }
                }

                // Handle tool calls in assistant message
                foreach (NeutralToolCallContent tc in message.Contents.OfType<NeutralToolCallContent>())
                {
                    input.Add(new JsonObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = tc.Id,
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Parameters
                    });
                }

                // Handle text content
                List<NeutralContent> nonToolCallContents = [.. message.Contents.Where(c => c is not NeutralToolCallContent && c is not NeutralThinkContent)];
                if (nonToolCallContents.Count > 0)
                {
                    input.Add(new JsonObject
                    {
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = ContentToOutputParts(nonToolCallContents)
                    });
                }
            }
            else if (message.Role == NeutralChatRole.Tool)
            {
                NeutralToolCallResponseContent? tcr = message.Contents.OfType<NeutralToolCallResponseContent>().FirstOrDefault();
                if (tcr != null)
                {
                    input.Add(new JsonObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = tcr.ToolCallId,
                        ["output"] = tcr.Response
                    });
                }
            }
        }

        return input;
    }

    private static JsonArray ContentToInputParts(IList<NeutralContent> contents)
    {
        JsonArray parts = [];
        foreach (NeutralContent content in contents)
        {
            if (content is NeutralTextContent text)
            {
                parts.Add(new JsonObject { ["type"] = "input_text", ["text"] = text.Content });
            }
            else if (content is NeutralFileUrlContent fileUrl)
            {
                parts.Add(new JsonObject { ["type"] = "input_image", ["image_url"] = fileUrl.Url });
            }
            else if (content is NeutralFileBlobContent blob)
            {
                parts.Add(new JsonObject
                {
                    ["type"] = "input_image",
                    ["image_url"] = $"data:{blob.MediaType};base64,{Convert.ToBase64String(blob.Data)}"
                });
            }
            else if (content is NeutralErrorContent error)
            {
                parts.Add(new JsonObject { ["type"] = "input_text", ["text"] = error.Content });
            }
        }
        return parts;
    }

    private static JsonArray ContentToOutputParts(IList<NeutralContent> contents)
    {
        JsonArray parts = [];
        foreach (NeutralContent content in contents)
        {
            if (content is NeutralTextContent text)
            {
                parts.Add(new JsonObject { ["type"] = "output_text", ["text"] = text.Content });
            }
            else if (content is NeutralErrorContent error)
            {
                parts.Add(new JsonObject { ["type"] = "output_text", ["text"] = error.Content });
            }
        }
        return parts;
    }

    public override async Task<string[]> ListModels(ModelKey modelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        string endpoint = GetEndpoint(modelKey);

        using HttpRequestMessage request = new(HttpMethod.Get, $"{endpoint}/v1/models");
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
}
