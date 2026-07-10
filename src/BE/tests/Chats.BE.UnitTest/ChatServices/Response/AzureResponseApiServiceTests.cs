using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.OpenAI.Special;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Chats.BE.UnitTest.ChatServices.Response;

public class AzureResponseApiServiceTests
{
    private const string TestDataPath = "ChatServices/Response/FiddlerDump";

    private sealed class CapturingHttpClientFactory(HttpStatusCode statusCode, string responseBody, Action<HttpRequestMessage> onRequest) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new CapturingHandler(statusCode, responseBody, onRequest));
        }

        private sealed class CapturingHandler(HttpStatusCode statusCode, string responseBody, Action<HttpRequestMessage> onRequest) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                onRequest(request);

                HttpResponseMessage resp = new(statusCode)
                {
                    Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(responseBody)))
                };

                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream")
                {
                    CharSet = "utf-8"
                };

                return Task.FromResult(resp);
            }
        }
    }

    private static ChatRequest CreateBaseChatRequest()
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://redacted.openai.azure.com",
            ModelProviderId = (short)DBModelProvider.AzureAIFoundry,
            CreatedAt = now,
        };

        ModelKey modelKey = new()
        {
            Id = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CurrentSnapshotId = modelKeySnapshot.Id,
            CurrentSnapshot = modelKeySnapshot,
        };

        modelKeySnapshot.ModelKey = modelKey;

        ModelSnapshot modelSnapshot = new()
        {
            Id = 21,
            ModelId = 1,
            Name = "Test Model",
            DeploymentName = "gpt-5.2",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowStreaming = true,
            ApiTypeId = (byte)DBApiType.OpenAIResponse,
            UseAsyncApi = false,
            CreatedAt = now,
        };

        Model model = new()
        {
            Id = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CurrentSnapshotId = modelSnapshot.Id,
            CurrentSnapshot = modelSnapshot,
        };

        modelSnapshot.Model = model;

        ChatConfig chatConfig = new()
        {
            Id = 1,
            ModelId = 1,
            Model = model,
            Temperature = 1,
            Effort = ReasoningEfforts.High,
            SystemPrompt = "你是AI助手Sdcb Chats\n当前日期: 2026/01/07，当前模型：gpt-5.2",
        };

        return new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("你作为gpt-5.2，是不是对openai response api了如指掌？")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            EndUserId = "8",
        };
    }

    [Fact]
    public async Task ResponseApiService_ShouldUseAzureOpenAIPrefixInRequestUri()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        Uri? requestUri = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            requestUri = req.RequestUri;
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.NotNull(requestUri);
        Assert.Equal("https://redacted.openai.azure.com/openai/v1/responses", requestUri!.ToString());
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendIncludeReasoningEncryptedContent()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;

        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        Assert.True(doc.RootElement.TryGetProperty("include", out JsonElement includeEl), "Request body should contain include.");
        Assert.Equal(JsonValueKind.Array, includeEl.ValueKind);

        List<string?> items = includeEl.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("reasoning.encrypted_content", items);
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendThinkingSignatureAsReasoningEncryptedContent()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(NeutralThinkContent.Create("t", signature: "sig_123")),
                NeutralMessage.FromUserText("hi")
            ]
        };

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement input = doc.RootElement.GetProperty("input");

        bool found = false;
        foreach (JsonElement item in input.EnumerateArray())
        {
            if (item.TryGetProperty("type", out JsonElement typeEl) && typeEl.GetString() == "reasoning")
            {
                if (item.TryGetProperty("encrypted_content", out JsonElement encEl) && encEl.GetString() == "sig_123")
                {
                    found = true;
                    break;
                }
            }
        }

        Assert.True(found, "Request input should contain a reasoning item with encrypted_content from NeutralThinkContent.Signature.");
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendOuterAndInnerSystemMessagesInOrder()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Messages =
            [
                NeutralMessage.FromUserText("first user"),
                NeutralMessage.FromSystemText("inner system"),
                NeutralMessage.FromUserText("second user")
            ]
        };

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement input = doc.RootElement.GetProperty("input");
        JsonElement[] messages = input.EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "message")
            .ToArray();

        Assert.Equal(["system", "user", "system", "user"], messages.Select(x => x.GetProperty("role").GetString()!).ToArray());
        Assert.Equal("你是AI助手Sdcb Chats\n当前日期: 2026/01/07，当前模型：gpt-5.2", messages[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("first user", messages[1].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("inner system", messages[2].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("second user", messages[3].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendHostedWebSearchTool_WhenSearchIsEnabled()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();
        request.ChatConfig.Model.CurrentSnapshot.AllowSearch = true;
        request.ChatConfig.WebSearchEnabled = true;

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement root = doc.RootElement;
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal("8", root.GetProperty("prompt_cache_key").GetString());
        Assert.Equal("24h", root.GetProperty("prompt_cache_retention").GetString());

        JsonElement webSearchTool = root.GetProperty("tools").EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "web_search");
        Assert.Equal("low", webSearchTool.GetProperty("search_context_size").GetString());
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendFunctionAndHostedWebSearchToolsTogether()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Tools = [FunctionTool.Create("run_code", "Run code", "{\"type\":\"object\"}")]
        };
        request.ChatConfig.Model.CurrentSnapshot.AllowSearch = true;
        request.ChatConfig.WebSearchEnabled = true;

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement[] tools = doc.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Contains(tools, x => x.GetProperty("type").GetString() == "function" && x.GetProperty("name").GetString() == "run_code");
        Assert.Contains(tools, x => x.GetProperty("type").GetString() == "web_search");
    }

    [Fact]
    public async Task ResponseApiService_ShouldNotSendHostedWebSearchTool_WhenModelDisallowsSearch()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();
        request.ChatConfig.Model.CurrentSnapshot.AllowSearch = false;
        request.ChatConfig.WebSearchEnabled = true;

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        Assert.False(doc.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task ResponseApiService_ShouldPutEncryptedContentIntoThinkChatSegmentSignature_OnOutputItemDone()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "AzureResponse.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);

        // SSE requires newlines between events, but FiddlerHttpDumpParser strips them.
        List<string> chunksWithNewlines = dump.Response.Chunks.Select(c => c + "\n").ToList();
        FiddlerDumpHttpClientFactory httpClientFactory = new(chunksWithNewlines, (HttpStatusCode)dump.Response.StatusCode, expectedRequestBody: null);

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();

        string expectedEncrypted = ExtractEncryptedContentFromDump(filePath);

        // Act
        List<ChatSegment> segments = [];
        await foreach (ChatSegment seg in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(seg);
        }

        // Assert
        ThinkChatSegment? thinkWithSig = segments.OfType<ThinkChatSegment>().FirstOrDefault(x => !string.IsNullOrEmpty(x.Signature));
        Assert.NotNull(thinkWithSig);
        Assert.Equal(expectedEncrypted, thinkWithSig!.Signature);
    }

    [Fact]
    public async Task ResponseApiService_ShouldParseHostedWebSearchDumpIntoSegments()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "AzureResponseWebSearch-Attempt2.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        List<string> chunksWithNewlines = dump.Response.Chunks.Select(c => c + "\n").ToList();
        FiddlerDumpHttpClientFactory httpClientFactory = new(chunksWithNewlines, (HttpStatusCode)dump.Response.StatusCode, expectedRequestBody: null);

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();

        // Act
        List<ChatSegment> segments = [];
        await foreach (ChatSegment seg in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(seg);
        }

        // Assert
        ToolCallSegment[] webSearchCalls = segments.OfType<ToolCallSegment>()
            .Where(x => x.Name == "web_search_call")
            .ToArray();
        Assert.Equal(4, webSearchCalls.Length);

        string[] actionTypes = webSearchCalls.Select(x =>
        {
            using JsonDocument args = JsonDocument.Parse(x.Arguments!);
            return args.RootElement.GetProperty("action").GetProperty("type").GetString()!;
        }).ToArray();
        Assert.Contains("search", actionTypes);
        Assert.Contains("open_page", actionTypes);
        Assert.Contains("find_in_page", actionTypes);

        ToolCallResponseSegment[] responses = segments.OfType<ToolCallResponseSegment>().ToArray();
        Assert.Equal(webSearchCalls.Select(x => x.Id).Order(), responses.Select(x => x.ToolCallId).Order());
    }

    [Fact]
    public async Task ResponseApiService_ShouldParseHostedWebSearchAnnotationsIntoToolResponse()
    {
        // Arrange
        string sse =
            "event: response.output_item.done\n" +
            "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"ws_1\",\"type\":\"web_search_call\",\"status\":\"completed\",\"action\":{\"type\":\"search\",\"query\":\"q\",\"queries\":[\"q\"]}},\"output_index\":0}\n\n" +
            "event: response.output_text.annotation.added\n" +
            "data: {\"type\":\"response.output_text.annotation.added\",\"annotation\":{\"type\":\"url_citation\",\"title\":\"Web search\",\"url\":\"https://developers.openai.com/api/docs/guides/tools-web-search\",\"start_index\":0,\"end_index\":10}}\n\n" +
            "event: response.output_item.done\n" +
            "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"msg_1\",\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"answer\",\"annotations\":[{\"type\":\"url_citation\",\"title\":\"Web search\",\"url\":\"https://developers.openai.com/api/docs/guides/tools-web-search\",\"start_index\":0,\"end_index\":10}]}]}}\n\n" +
            "event: response.completed\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":1,\"input_tokens_details\":{\"cached_tokens\":16000},\"output_tokens\":1}}}\n\n";

        FiddlerDumpHttpClientFactory httpClientFactory = new([sse], HttpStatusCode.OK, expectedRequestBody: null);
        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();

        // Act
        List<ChatSegment> segments = [];
        await foreach (ChatSegment seg in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(seg);
        }

        // Assert
        ToolCallSegment call = Assert.Single(segments.OfType<ToolCallSegment>());
        Assert.Equal("web_search_call", call.Name);

        ToolCallResponseSegment response = Assert.Single(segments.OfType<ToolCallResponseSegment>());
        Assert.Equal("ws_1", response.ToolCallId);
        using JsonDocument responseJson = JsonDocument.Parse(response.Response!);
        JsonElement result = Assert.Single(responseJson.RootElement.EnumerateArray());
        Assert.Equal("web_search_result", result.GetProperty("type").GetString());
        Assert.Equal("Web search", result.GetProperty("title").GetString());
        Assert.Equal("https://developers.openai.com/api/docs/guides/tools-web-search", result.GetProperty("url").GetString());

        UsageChatSegment usage = Assert.IsType<UsageChatSegment>(segments.First(x => x is UsageChatSegment));
        Assert.Equal(16000, usage.Usage.CacheTokens);
        FinishReasonChatSegment finish = Assert.IsType<FinishReasonChatSegment>(segments.Last(x => x is FinishReasonChatSegment));
        Assert.Equal(DBFinishReason.Success, finish.FinishReason);
    }

    [Fact]
    public async Task ResponseApiService_ShouldIgnoreArrayTypeProperties_WhenScanningUrlCitations()
    {
        // Arrange
        string sse =
            "event: response.output_item.done\n" +
            "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"ws_1\",\"type\":\"web_search_call\",\"status\":\"completed\",\"action\":{\"type\":\"search\",\"query\":\"q\",\"queries\":[\"q\"]}},\"output_index\":0}\n\n" +
            "event: response.completed\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"tools\":[{\"type\":\"function\",\"parameters\":{\"properties\":{\"file\":{\"type\":[\"string\",\"null\"]}}}}],\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"answer\",\"annotations\":[{\"type\":\"url_citation\",\"title\":\"Source\",\"url\":\"https://example.com\",\"start_index\":0,\"end_index\":6}]}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}}\n\n";

        FiddlerDumpHttpClientFactory httpClientFactory = new([sse], HttpStatusCode.OK, expectedRequestBody: null);
        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest();

        // Act
        List<ChatSegment> segments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        ToolCallResponseSegment response = Assert.Single(segments.OfType<ToolCallResponseSegment>());
        using JsonDocument responseJson = JsonDocument.Parse(response.Response!);
        JsonElement result = Assert.Single(responseJson.RootElement.EnumerateArray());
        Assert.Equal("Source", result.GetProperty("title").GetString());
        Assert.Equal("https://example.com", result.GetProperty("url").GetString());

        FinishReasonChatSegment finish = Assert.IsType<FinishReasonChatSegment>(segments.Last(x => x is FinishReasonChatSegment));
        Assert.Equal(DBFinishReason.Success, finish.FinishReason);
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendAllFunctionCallOutputs_FromSingleToolMessage()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralToolCallContent.Create("call_1", "Glob", "{\"pattern\":\"**/*.cs\"}"),
                    NeutralToolCallContent.Create("call_2", "Glob", "{\"pattern\":\"**/*.{ts,tsx}\"}")
                ),
                NeutralMessage.FromTool(
                    NeutralToolCallResponseContent.Create("call_1", "csharp files"),
                    NeutralToolCallResponseContent.Create("call_2", "typescript files")
                )
            ]
        };

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement input = doc.RootElement.GetProperty("input");

        List<string> functionCallOutputIds = [];
        foreach (JsonElement item in input.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out JsonElement typeEl) || typeEl.GetString() != "function_call_output")
            {
                continue;
            }

            functionCallOutputIds.Add(item.GetProperty("call_id").GetString()!);
        }

        Assert.Equal(["call_1", "call_2"], functionCallOutputIds);
    }

    [Fact]
    public async Task ResponseApiService_ShouldReplayPreviousWebSearchCall_AsHostedResponseItem()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralToolCallContent.Create(
                        "ws_1",
                        "web_search_call",
                        "{\"type\":\"web_search_call\",\"status\":\"completed\",\"action\":{\"type\":\"search\",\"query\":\"OpenAI Responses API web_search\",\"queries\":[\"OpenAI Responses API web_search\"]}}"),
                    NeutralTextContent.Create("旧答案")
                ),
                NeutralMessage.FromTool(
                    NeutralToolCallResponseContent.Create(
                        "ws_1",
                        "[{\"type\":\"web_search_result\",\"title\":\"Web search\",\"url\":\"https://developers.openai.com/api/docs/guides/tools-web-search\"}]")
                ),
                NeutralMessage.FromUserText("继续")
            ]
        };

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement input = doc.RootElement.GetProperty("input");

        JsonElement webSearchCall = input.EnumerateArray()
            .Single(item => item.GetProperty("type").GetString() == "web_search_call");
        Assert.Equal("ws_1", webSearchCall.GetProperty("id").GetString());
        Assert.Equal("completed", webSearchCall.GetProperty("status").GetString());
        Assert.Equal("search", webSearchCall.GetProperty("action").GetProperty("type").GetString());

        Assert.DoesNotContain(input.EnumerateArray(), item =>
            item.GetProperty("type").GetString() == "function_call"
            && item.TryGetProperty("name", out JsonElement nameEl)
            && nameEl.GetString() == "web_search_call");
        Assert.DoesNotContain(input.EnumerateArray(), item =>
            item.GetProperty("type").GetString() == "function_call_output"
            && item.TryGetProperty("call_id", out JsonElement callIdEl)
            && callIdEl.GetString() == "ws_1");
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendImageParts_InFunctionCallOutput()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralToolCallContent.Create("call_1", "draw_chart", "{}")
                ),
                NeutralMessage.FromTool(
                    NeutralToolCallResponseContent.Create("call_1", "chart generated"),
                    NeutralFileUrlContent.Create("https://example.com/chart.png")
                )
            ]
        };

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement input = doc.RootElement.GetProperty("input");
        JsonElement toolOutput = input.EnumerateArray()
            .First(item => item.GetProperty("type").GetString() == "function_call_output");

        Assert.Equal("call_1", toolOutput.GetProperty("call_id").GetString());

        JsonElement output = toolOutput.GetProperty("output");
        Assert.Equal(JsonValueKind.Array, output.ValueKind);

        JsonElement[] parts = output.EnumerateArray().ToArray();
        Assert.Equal("input_text", parts[0].GetProperty("type").GetString());
        Assert.Equal("chart generated", parts[0].GetProperty("text").GetString());
        Assert.Equal("input_image", parts[1].GetProperty("type").GetString());
        Assert.Equal("https://example.com/chart.png", parts[1].GetProperty("image_url").GetString());
    }

    [Fact]
    public async Task ResponseApiService_ShouldSendEmptyJsonObjectString_ForEmptyToolArguments()
    {
        // Arrange
        string sse = "event: response.completed\n" +
                     "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":0,\"output_tokens\":0}}}\n\n";

        string? capturedBody = null;
        CapturingHttpClientFactory httpClientFactory = new(HttpStatusCode.OK, sse, req =>
        {
            capturedBody = req.Content == null ? null : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        AzureResponseApiService service = new(httpClientFactory, NullLogger<AzureResponseApiService>.Instance);
        ChatRequest request = CreateBaseChatRequest() with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralToolCallContent.Create("call_1", "create_docker_session", "")
                )
            ]
        };

        // Act
        await foreach (ChatSegment _ in service.ChatStreamed(request, CancellationToken.None))
        {
            // drain
        }

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement input = doc.RootElement.GetProperty("input");
        JsonElement functionCall = input.EnumerateArray()
            .First(item => item.GetProperty("type").GetString() == "function_call");

        Assert.Equal("{}", functionCall.GetProperty("arguments").GetString());
    }

    private static string ExtractEncryptedContentFromDump(string dumpFilePath)
    {
        foreach (string line in System.IO.File.ReadLines(dumpFilePath))
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.Contains("\"type\":\"response.output_item.done\"", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.Contains("\"encrypted_content\"", StringComparison.Ordinal))
            {
                continue;
            }

            string json = line["data: ".Length..];
            using JsonDocument doc = JsonDocument.Parse(json);

            JsonElement item = doc.RootElement.GetProperty("item");
            string? itemType = item.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : null;
            if (!string.Equals(itemType, "reasoning", StringComparison.Ordinal))
            {
                continue;
            }

            string? encrypted = item.TryGetProperty("encrypted_content", out JsonElement encEl) ? encEl.GetString() : null;
            if (!string.IsNullOrEmpty(encrypted))
            {
                return encrypted;
            }
        }

        throw new InvalidOperationException("Could not find reasoning encrypted_content in response.output_item.done event.");
    }
}
