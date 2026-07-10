using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.RequestTracing;
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
    private const string WebSearchHostedToolName = "web_search";
    private const string WebSearchCallType = "web_search_call";
    private const string WebSearchContextSize = "low";

    private sealed record WebSearchCall(string Id, string Status, JsonObject? Action)
    {
        public bool IsSearch => string.Equals(Action?["type"]?.GetValue<string>(), "search", StringComparison.Ordinal);

        public string ToToolArguments()
        {
            JsonObject args = new()
            {
                ["type"] = WebSearchCallType,
                ["status"] = Status,
            };
            if (Action != null)
            {
                args["action"] = Action.DeepClone();
            }
            return args.ToJsonString(JSON.JsonSerializerOptions);
        }
    }

    protected override HashSet<string> SupportedContentTypes =>
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    ];

    protected virtual string GetEndpoint(ModelKeySnapshot modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DBModelProvider)modelKey.ModelProviderId);
        }
        return host?.TrimEnd('/') ?? "";
    }

    protected virtual string GetEndpoint(Model model)
    {
        return ModelRequestOverrides.ResolveEndpoint(model.CurrentSnapshot);
    }

    protected virtual void AddAuthorizationHeader(HttpRequestMessage request, ModelKeySnapshot modelKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", modelKey.Secret);
    }

    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Model model = request.ChatConfig.Model;
        ModelKeySnapshot modelKey = model.CurrentSnapshot.ModelKeySnapshot;
        string endpoint = GetEndpoint(model);
        bool hasTools = false;

        if (request.ChatConfig.Model.CurrentSnapshot.UseAsyncApi)
        {
            // Background mode
            Stopwatch sw = Stopwatch.StartNew();
            JsonObject requestBody = BuildRequestBody(request, stream: false, background: true);
            ModelRequestOverrides.ApplyBody(requestBody, model.CurrentSnapshot);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/responses");
            AddAuthorizationHeader(httpRequest, modelKey);
            httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");
            ModelRequestOverrides.ApplyHeaders(httpRequest, model.CurrentSnapshot);

            using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceResponseApi);
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
                        AddAuthorizationHeader(cancelRequest, modelKey);
                        using HttpClient cancelClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceResponseApi);
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
                    AddAuthorizationHeader(getRequest, modelKey);

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
                int webSearchIndex = 100000;
                List<WebSearchCall> webSearchCalls = [];
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
                        else if (itemType == WebSearchCallType)
                        {
                            WebSearchCall? call = ParseWebSearchCall(item);
                            if (call != null)
                            {
                                AddOrReplaceWebSearchCall(webSearchCalls, call);
                                yield return new ToolCallSegment
                                {
                                    Index = webSearchIndex++,
                                    Id = call.Id,
                                    Name = WebSearchCallType,
                                    Arguments = call.ToToolArguments(),
                                };
                            }
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

                foreach (ChatSegment segment in CreateWebSearchToolResponses(webSearchCalls, ExtractUrlCitations(outputItems)))
                {
                    yield return segment;
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
            ModelRequestOverrides.ApplyBody(requestBody, model.CurrentSnapshot);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{endpoint}/v1/responses");
            AddAuthorizationHeader(httpRequest, modelKey);
            httpRequest.Content = new StringContent(requestBody.ToJsonString(JSON.JsonSerializerOptions), Encoding.UTF8, "application/json");
            ModelRequestOverrides.ApplyHeaders(httpRequest, model.CurrentSnapshot);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceResponseApi);
            httpClient.Timeout = NetworkTimeout;

            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new RawChatServiceException((int)response.StatusCode, errorBody);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            int fcIndex = 0;
            int webSearchIndex = 100000;
            string? currentFcId = null;
            string? currentFcName = null;
            List<WebSearchCall> webSearchCalls = [];
            JsonArray webSearchCitations = [];
            bool emittedWebSearchResponses = false;

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

                AppendJsonArray(webSearchCitations, ExtractUrlCitationsRecursive(json));

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
                else if (eventType == "response.output_text.annotation.added")
                {
                    if (json.TryGetProperty("annotation", out JsonElement annotationEl)
                        && annotationEl.TryGetProperty("type", out JsonElement annotationTypeEl)
                        && annotationTypeEl.GetString() == "url_citation")
                    {
                        JsonArray singleCitation = [CreateWebSearchResult(annotationEl)];
                        AppendJsonArray(webSearchCitations, singleCitation);
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

                    if (responseEl?.TryGetProperty("output", out JsonElement outputEl) == true)
                    {
                        AddWebSearchCallsFromOutput(outputEl, webSearchCalls);
                        AppendJsonArray(webSearchCitations, ExtractUrlCitations(outputEl));
                    }

                    if (!emittedWebSearchResponses)
                    {
                        foreach (ChatSegment segment in CreateWebSearchToolResponses(webSearchCalls, webSearchCitations))
                        {
                            yield return segment;
                        }
                        emittedWebSearchResponses = true;
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
                        else if (itemType == WebSearchCallType)
                        {
                            WebSearchCall? call = ParseWebSearchCall(itemEl);
                            if (call != null)
                            {
                                AddOrReplaceWebSearchCall(webSearchCalls, call);
                                yield return new ToolCallSegment
                                {
                                    Index = webSearchIndex++,
                                    Id = call.Id,
                                    Name = WebSearchCallType,
                                    Arguments = call.ToToolArguments(),
                                };
                            }
                        }
                        else if (itemType == "message")
                        {
                            AppendJsonArray(webSearchCitations, ExtractUrlCitationsFromMessage(itemEl));
                            if (!emittedWebSearchResponses)
                            {
                                foreach (ChatSegment segment in CreateWebSearchToolResponses(webSearchCalls, webSearchCitations))
                                {
                                    yield return segment;
                                }
                                emittedWebSearchResponses = true;
                            }
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
                else if (eventType == "response.content_part.done")
                {
                    if (json.TryGetProperty("part", out JsonElement partEl))
                    {
                        AppendJsonArray(webSearchCitations, ExtractUrlCitationsFromContentPart(partEl));
                    }
                }
            }
        }
    }

    private static WebSearchCall? ParseWebSearchCall(JsonNode? item)
    {
        if (item?["type"]?.GetValue<string>() != WebSearchCallType)
        {
            return null;
        }

        string? id = item["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string status = item["status"]?.GetValue<string>() ?? "completed";
        JsonObject? action = item["action"]?.DeepClone() as JsonObject;
        return new WebSearchCall(id, status, action);
    }

    private static WebSearchCall? ParseWebSearchCall(JsonElement item)
    {
        if (!item.TryGetProperty("type", out JsonElement typeEl) || typeEl.GetString() != WebSearchCallType)
        {
            return null;
        }

        string? id = item.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string status = item.TryGetProperty("status", out JsonElement statusEl) ? statusEl.GetString() ?? "completed" : "completed";
        JsonObject? action = null;
        if (item.TryGetProperty("action", out JsonElement actionEl) && actionEl.ValueKind == JsonValueKind.Object)
        {
            action = JsonNode.Parse(actionEl.GetRawText()) as JsonObject;
        }

        return new WebSearchCall(id, status, action);
    }

    private static void AddOrReplaceWebSearchCall(List<WebSearchCall> calls, WebSearchCall call)
    {
        int index = calls.FindIndex(x => x.Id == call.Id);
        if (index >= 0)
        {
            calls[index] = call;
        }
        else
        {
            calls.Add(call);
        }
    }

    private static void AddWebSearchCallsFromOutput(JsonElement outputEl, List<WebSearchCall> calls)
    {
        if (outputEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement item in outputEl.EnumerateArray())
        {
            WebSearchCall? call = ParseWebSearchCall(item);
            if (call != null)
            {
                AddOrReplaceWebSearchCall(calls, call);
            }
        }
    }

    private static JsonArray ExtractUrlCitations(JsonArray? outputItems)
    {
        JsonArray citations = [];
        if (outputItems == null)
        {
            return citations;
        }

        foreach (JsonNode? item in outputItems)
        {
            if (item?["type"]?.GetValue<string>() != "message")
            {
                continue;
            }

            JsonArray? contentArray = item["content"]?.AsArray();
            if (contentArray == null)
            {
                continue;
            }

            foreach (JsonNode? content in contentArray)
            {
                JsonArray? annotations = content?["annotations"]?.AsArray();
                if (annotations == null)
                {
                    continue;
                }

                foreach (JsonNode? annotation in annotations)
                {
                    if (annotation?["type"]?.GetValue<string>() == "url_citation")
                    {
                        citations.Add(CreateWebSearchResult(annotation));
                    }
                }
            }
        }

        return citations;
    }

    private static JsonArray ExtractUrlCitations(JsonElement outputEl)
    {
        JsonArray citations = [];
        if (outputEl.ValueKind != JsonValueKind.Array)
        {
            return citations;
        }

        foreach (JsonElement item in outputEl.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out JsonElement typeEl) || typeEl.GetString() != "message")
            {
                continue;
            }

            if (!item.TryGetProperty("content", out JsonElement contentEl) || contentEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement content in contentEl.EnumerateArray())
            {
                if (!content.TryGetProperty("annotations", out JsonElement annotationsEl) || annotationsEl.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement annotation in annotationsEl.EnumerateArray())
                {
                    if (annotation.TryGetProperty("type", out JsonElement annotationTypeEl)
                        && annotationTypeEl.ValueKind == JsonValueKind.String
                        && annotationTypeEl.GetString() == "url_citation")
                    {
                        citations.Add(CreateWebSearchResult(annotation));
                    }
                }
            }
        }

        return citations;
    }

    private static JsonArray ExtractUrlCitationsFromMessage(JsonElement messageEl)
    {
        JsonArray citations = [];
        if (!messageEl.TryGetProperty("content", out JsonElement contentEl) || contentEl.ValueKind != JsonValueKind.Array)
        {
            return citations;
        }

        foreach (JsonElement content in contentEl.EnumerateArray())
        {
            AppendJsonArray(citations, ExtractUrlCitationsFromContentPart(content));
        }

        return citations;
    }

    private static JsonArray ExtractUrlCitationsFromContentPart(JsonElement content)
    {
        JsonArray citations = [];
        if (!content.TryGetProperty("annotations", out JsonElement annotationsEl) || annotationsEl.ValueKind != JsonValueKind.Array)
        {
            return citations;
        }

        foreach (JsonElement annotation in annotationsEl.EnumerateArray())
        {
            if (annotation.TryGetProperty("type", out JsonElement annotationTypeEl)
                && annotationTypeEl.ValueKind == JsonValueKind.String
                && annotationTypeEl.GetString() == "url_citation")
            {
                citations.Add(CreateWebSearchResult(annotation));
            }
        }

        return citations;
    }

    private static JsonArray ExtractUrlCitationsRecursive(JsonElement element)
    {
        JsonArray citations = [];
        AddUrlCitationsRecursive(element, citations);
        return citations;
    }

    private static void AddUrlCitationsRecursive(JsonElement element, JsonArray citations)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && typeEl.GetString() == "url_citation")
            {
                citations.Add(CreateWebSearchResult(element));
                return;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                AddUrlCitationsRecursive(property.Value, citations);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                AddUrlCitationsRecursive(item, citations);
            }
        }
    }

    private static void AppendJsonArray(JsonArray target, JsonArray source)
    {
        foreach (JsonNode? item in source)
        {
            string? itemJson = item?.ToJsonString(JSON.JsonSerializerOptions);
            if (itemJson != null && target.Any(existing => existing?.ToJsonString(JSON.JsonSerializerOptions) == itemJson))
            {
                continue;
            }
            target.Add(item?.DeepClone());
        }
    }

    private static JsonObject CreateWebSearchResult(JsonNode annotation)
    {
        JsonObject result = new()
        {
            ["type"] = "web_search_result",
            ["title"] = annotation["title"]?.GetValue<string>(),
            ["url"] = annotation["url"]?.GetValue<string>(),
            ["page_age"] = null,
        };
        if (annotation["start_index"] != null)
        {
            result["start_index"] = annotation["start_index"]!.GetValue<int>();
        }
        if (annotation["end_index"] != null)
        {
            result["end_index"] = annotation["end_index"]!.GetValue<int>();
        }
        return result;
    }

    private static JsonObject CreateWebSearchResult(JsonElement annotation)
    {
        JsonObject result = new()
        {
            ["type"] = "web_search_result",
            ["title"] = annotation.TryGetProperty("title", out JsonElement titleEl) ? titleEl.GetString() : null,
            ["url"] = annotation.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null,
            ["page_age"] = null,
        };
        if (annotation.TryGetProperty("start_index", out JsonElement startEl) && startEl.ValueKind == JsonValueKind.Number)
        {
            result["start_index"] = startEl.GetInt32();
        }
        if (annotation.TryGetProperty("end_index", out JsonElement endEl) && endEl.ValueKind == JsonValueKind.Number)
        {
            result["end_index"] = endEl.GetInt32();
        }
        return result;
    }

    private static IEnumerable<ChatSegment> CreateWebSearchToolResponses(IReadOnlyList<WebSearchCall> calls, JsonArray citations)
    {
        if (calls.Count == 0)
        {
            yield break;
        }

        WebSearchCall citationOwner = calls.FirstOrDefault(x => x.IsSearch) ?? calls[0];
        foreach (WebSearchCall call in calls)
        {
            JsonArray response = call.Id == citationOwner.Id ? CloneJsonArray(citations) : [];
            yield return ChatSegment.FromToolCallResponse(call.Id, response.ToJsonString(JSON.JsonSerializerOptions), 0, true);
        }
    }

    private static JsonArray CloneJsonArray(JsonArray array)
    {
        JsonArray clone = [];
        foreach (JsonNode? item in array)
        {
            clone.Add(item?.DeepClone());
        }
        return clone;
    }

    private static bool TryCreateWebSearchCallInput(NeutralToolCallContent toolCall, out JsonObject? webSearchCall)
    {
        webSearchCall = null;
        try
        {
            JsonNode? node = JsonNode.Parse(NormalizeToolCallArguments(toolCall.Parameters));
            if (node is not JsonObject args || args["type"]?.GetValue<string>() != WebSearchCallType)
            {
                return false;
            }

            webSearchCall = new JsonObject
            {
                ["type"] = WebSearchCallType,
                ["id"] = toolCall.Id,
                ["status"] = args["status"]?.GetValue<string>() ?? "completed",
            };
            if (args["action"] is JsonObject action)
            {
                webSearchCall["action"] = action.DeepClone();
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
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
            ["model"] = request.ChatConfig.Model.CurrentSnapshot.DeploymentName,
            ["input"] = BuildInputArray(request),
            ["stream"] = stream,
            ["store"] = false,
        };

        if (request.ChatConfig.Temperature != null)
        {
            body["temperature"] = request.ChatConfig.Temperature.Value;
        }

        if (request.EndUserId != null)
        {
            body["prompt_cache_key"] = request.EndUserId;
            body["prompt_cache_retention"] = "24h";
        }

        if (request.ChatConfig.MaxOutputTokens != null)
        {
            if (request.ChatConfig.MaxOutputTokens.Value < 16)
            {
                // Invalid 'max_output_tokens': integer below minimum value. Expected a value >= 16, but got 1 instead.
                body["max_output_tokens"] = 16;
            }
            else
            {
                body["max_output_tokens"] = request.ChatConfig.MaxOutputTokens.Value;
            }
        }

        // Reasoning options - only add if explicitly specified
        string? reasoningEffort = request.ChatConfig.Effort;
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
        if (request.ChatConfig.Model.CurrentSnapshot.AllowSearch && request.ChatConfig.WebSearchEnabled)
        {
            functionTools.Add(new JsonObject
            {
                ["type"] = WebSearchHostedToolName,
                ["search_context_size"] = WebSearchContextSize,
            });
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
        HashSet<string> webSearchToolCallIds = [];

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
            if (message.Role == NeutralChatRole.System)
            {
                input.Add(new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "system",
                    ["content"] = ContentToInputParts(message.Contents)
                });
            }
            else if (message.Role == NeutralChatRole.User)
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
                    if (string.Equals(tc.Name, WebSearchCallType, StringComparison.Ordinal))
                    {
                        if (TryCreateWebSearchCallInput(tc, out JsonObject? webSearchCall))
                        {
                            webSearchToolCallIds.Add(tc.Id);
                            input.Add(webSearchCall);
                        }
                        continue;
                    }

                    input.Add(new JsonObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = tc.Id,
                        ["name"] = tc.Name,
                        ["arguments"] = NormalizeToolCallArguments(tc.Parameters)
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
                IReadOnlyList<NeutralToolResponseGroup> toolResponseGroups = message.GetToolResponseGroups();
                if (toolResponseGroups.Count == 0)
                {
                    throw new CustomChatServiceException(DBFinishReason.InternalConfigIssue, "Tool message does not contain any tool response content.");
                }

                foreach (NeutralToolResponseGroup group in toolResponseGroups)
                {
                    if (webSearchToolCallIds.Contains(group.ToolResponse.ToolCallId))
                    {
                        continue;
                    }
                    input.Add(CreateFunctionCallOutput(group));
                }
            }
        }

        return input;
    }

    private static string NormalizeToolCallArguments(string? parameters)
    {
        return string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters;
    }

    private static JsonObject CreateFunctionCallOutput(NeutralToolResponseGroup group)
    {
        JsonObject output = new()
        {
            ["type"] = "function_call_output",
            ["call_id"] = group.ToolResponse.ToolCallId,
        };

        if (group.AttachedContents.Count == 0)
        {
            output["output"] = group.ToolResponse.Response;
            return output;
        }

        List<NeutralContent> partsContent = [];
        if (!string.IsNullOrEmpty(group.ToolResponse.Response))
        {
            partsContent.Add(NeutralTextContent.Create(group.ToolResponse.Response));
        }
        partsContent.AddRange(group.AttachedContents);

        JsonArray parts = ContentToInputParts(partsContent);
        output["output"] = parts.Count > 0 ? parts : group.ToolResponse.Response;
        return output;
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

    public override async Task<string[]> ListModels(ModelKeySnapshot modelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey.Secret, nameof(modelKey.Secret));

        string endpoint = GetEndpoint(modelKey);

        using HttpRequestMessage request = new(HttpMethod.Get, $"{endpoint}/v1/models");
        AddAuthorizationHeader(request, modelKey);

        using HttpClient httpClient = httpClientFactory.CreateClient(HttpClientNames.ChatServiceResponseApi);
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
