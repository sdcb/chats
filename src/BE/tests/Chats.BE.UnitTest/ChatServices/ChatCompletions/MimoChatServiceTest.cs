using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Net;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;
using System.Text;
using System.Text.Json;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class MimoChatServiceTest
{
    private sealed class CapturingHttpClientFactory(string responseBody, Action<HttpRequestMessage> onRequest) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(responseBody, onRequest));

        private sealed class Handler(string responseBody, Action<HttpRequestMessage> onRequest) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                onRequest(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(responseBody))),
                });
            }
        }
    }

    private static ChatConfig CreateChatConfig()
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (short)DBModelProvider.Mimo,
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
            DeploymentName = "mimo-v2.5-pro",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowStreaming = true,
            ApiTypeId = (byte)DBApiType.OpenAIChatCompletion,
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

        return new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };
    }

    [Fact]
    public async Task Streaming_NormalToolCall_ShouldParseCorrectly()
    {
        const string sse = """
            data: {"id":"chat_1","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_13f2f94b48d240a8ae062fe0","function":{"name":"run_csharp","arguments":"1234.0 / "}}]},"finish_reason":null}]}

            data: {"id":"chat_1","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"5432.0"}}]},"finish_reason":null}]}

            data: {"id":"chat_1","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}

            data: [DONE]

            """;
        var httpClientFactory = new ReplayHttpClientFactory(sse);

        MimoChatService service = new(httpClientFactory);

        var chatConfig = CreateChatConfig();

        ChatRequest request = new()
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8"
        };

        // Act
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        List<ToolCallSegment> toolCalls = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCalls);
        
        var toolCall = toolCalls.First(tc => tc.Id != null);
        Assert.Equal("call_13f2f94b48d240a8ae062fe0", toolCall.Id);
        Assert.Equal("run_csharp", toolCall.Name);
        
        var allArguments = string.Join("", toolCalls.Where(tc => tc.Index == toolCall.Index).Select(tc => tc.Arguments));
        Assert.Contains("1234.0 / 5432.0", allArguments);

        var finishReason = segments.OfType<FinishReasonChatSegment>().LastOrDefault();
        Assert.NotNull(finishReason);
        Assert.Equal(DBFinishReason.ToolCalls, finishReason.FinishReason);
    }

    [Fact]
    public async Task SearchEnabled_ShouldAddMinimalHostedWebSearchTool()
    {
        string sse = "data: {\"id\":\"chat_1\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                     "data: [DONE]\n\n";
        string? capturedBody = null;
        CapturingHttpClientFactory factory = new(sse, request =>
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
        ChatConfig config = CreateChatConfig();
        config.Model.CurrentSnapshot.AllowSearch = true;
        config.WebSearchEnabled = true;
        MimoChatService service = new(factory);

        await foreach (ChatSegment _ in service.ChatStreamed(new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("news")],
            ChatConfig = config,
            Source = UsageSource.Api,
            Streamed = true,
            Tools = [FunctionTool.Create("lookup", "Lookup", "{\"type\":\"object\"}")],
        }, CancellationToken.None))
        {
        }

        Assert.NotNull(capturedBody);
        using JsonDocument doc = JsonDocument.Parse(capturedBody);
        JsonElement[] tools = doc.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Equal(2, tools.Length);
        JsonElement searchTool = Assert.Single(tools,
            x => x.GetProperty("type").GetString() == "web_search");
        Assert.Single(tools, x => x.GetProperty("type").GetString() == "function");
        Assert.Single(searchTool.EnumerateObject());
        Assert.Equal("auto", doc.RootElement.GetProperty("tool_choice").GetString());
    }

    [Fact]
    public async Task Streaming_SearchAnnotations_ShouldCreateStructuredToolResponse()
    {
        string sse =
            "data: {\"id\":\"chat_1\",\"choices\":[{\"index\":0,\"delta\":{\"annotations\":[{\"type\":\"url_citation\",\"title\":\"Source\",\"url\":\"https://example.com\",\"summary\":\"Summary\",\"site_name\":\"Example\"}]},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"chat_1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"answer\"},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n";
        CapturingHttpClientFactory factory = new(sse, _ => { });
        ChatConfig config = CreateChatConfig();
        config.Model.CurrentSnapshot.AllowSearch = true;
        config.WebSearchEnabled = true;
        MimoChatService service = new(factory);
        List<ChatSegment> segments = [];

        await foreach (ChatSegment segment in service.ChatStreamed(new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("news")],
            ChatConfig = config,
            Source = UsageSource.Api,
            Streamed = true,
        }, CancellationToken.None))
        {
            segments.Add(segment);
        }

        ToolCallSegment call = Assert.Single(segments.OfType<ToolCallSegment>());
        Assert.Equal("web_search_call", call.Name);
        ToolCallResponseSegment response = Assert.Single(segments.OfType<ToolCallResponseSegment>());
        Assert.Equal(call.Id, response.ToolCallId);
        Assert.True(segments.IndexOf(call) < segments.FindIndex(x => x is TextChatSegment));
        Assert.Equal(segments.IndexOf(call) + 1, segments.IndexOf(response));
        using JsonDocument result = JsonDocument.Parse(response.Response!);
        JsonElement citation = Assert.Single(result.RootElement.EnumerateArray());
        Assert.Equal("web_search_result", citation.GetProperty("type").GetString());
        Assert.Equal("https://example.com", citation.GetProperty("url").GetString());
        Assert.Equal(DBFinishReason.Stop, segments.OfType<FinishReasonChatSegment>().Single().FinishReason);
    }

    [Fact]
    public async Task NonStreaming_SearchAnnotations_ShouldCreateStructuredToolResponse()
    {
        string responseJson = """
            {"id":"chat_2","choices":[{"index":0,"message":{"role":"assistant","content":"answer","annotations":[{"type":"url_citation","title":"Source","url":"https://example.com"}]},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """;
        CapturingHttpClientFactory factory = new(responseJson, _ => { });
        ChatConfig config = CreateChatConfig();
        config.Model.CurrentSnapshot.AllowStreaming = false;
        config.Model.CurrentSnapshot.AllowSearch = true;
        config.WebSearchEnabled = true;
        MimoChatService service = new(factory);
        List<ChatSegment> segments = [];

        await foreach (ChatSegment segment in service.ChatStreamed(new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("news")],
            ChatConfig = config,
            Source = UsageSource.Api,
            Streamed = false,
        }, CancellationToken.None))
        {
            segments.Add(segment);
        }

        Assert.Single(segments.OfType<ToolCallSegment>());
        ToolCallResponseSegment response = Assert.Single(segments.OfType<ToolCallResponseSegment>());
        using JsonDocument result = JsonDocument.Parse(response.Response!);
        Assert.Equal("https://example.com", Assert.Single(result.RootElement.EnumerateArray()).GetProperty("url").GetString());
    }

    [Fact]
    public async Task Streaming_WebSearchDump_ShouldEmitSourcesBeforeReasoningAndAnswer()
    {
        const string sse = """
            data: {"id":"chat_search","choices":[{"index":0,"delta":{"annotations":[{"type":"url_citation","title":"Weather","url":"https://weather.example/","summary":"Sunny"},{"type":"url_citation","title":"Forecast","url":"https://forecast.example/","summary":"Warm"}]},"finish_reason":null}]}

            data: {"id":"chat_search","choices":[{"index":0,"delta":{"reasoning_content":"Looking up the forecast."},"finish_reason":null}]}

            data: {"id":"chat_search","choices":[{"index":0,"delta":{"content":"Tomorrow will be sunny."},"finish_reason":"stop"}]}

            data: [DONE]

            """;
        IHttpClientFactory factory = new ReplayHttpClientFactory(sse);
        ChatConfig config = CreateChatConfig();
        config.Model.CurrentSnapshot.AllowSearch = true;
        config.WebSearchEnabled = true;
        MimoChatService service = new(factory);
        List<ChatSegment> segments = [];

        await foreach (ChatSegment segment in service.ChatStreamed(new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("明天长沙天气怎么样？")],
            ChatConfig = config,
            Source = UsageSource.Api,
            Streamed = true,
        }, CancellationToken.None))
        {
            segments.Add(segment);
        }

        int callIndex = segments.FindIndex(x => x is ToolCallSegment { Name: "web_search_call" });
        int responseIndex = segments.FindIndex(x => x is ToolCallResponseSegment);
        int reasoningIndex = segments.FindIndex(x => x is ThinkChatSegment think && !string.IsNullOrEmpty(think.Think));
        int answerIndex = segments.FindIndex(x => x is TextChatSegment);
        Assert.True(callIndex >= 0);
        Assert.Equal(callIndex + 1, responseIndex);
        string order = string.Join(",", segments.Select((segment, index) => $"{index}:{segment.GetType().Name}"));
        Assert.True(responseIndex < reasoningIndex, order);
        Assert.True(responseIndex < answerIndex, order);

        ToolCallResponseSegment searchResponse = Assert.IsType<ToolCallResponseSegment>(segments[responseIndex]);
        using JsonDocument results = JsonDocument.Parse(searchResponse.Response!);
        string[] urls = results.RootElement.EnumerateArray().Select(x => x.GetProperty("url").GetString()!).ToArray();
        Assert.NotEmpty(urls);
        Assert.Equal(urls.Distinct(StringComparer.Ordinal).Count(), urls.Length);
    }
}
