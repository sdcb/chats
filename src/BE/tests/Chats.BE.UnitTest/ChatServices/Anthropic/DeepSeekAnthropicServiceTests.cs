using System.Net;
using Chats.BE.Controllers.Api.AnthropicCompatible.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.ChatServices.Anthropic;

public class DeepSeekAnthropicServiceTests
{
    private static IHttpClientFactory CreateMockHttpClientFactory(params string[] chunks)
    {
        return new ReplayHttpClientFactory(string.Concat(chunks), HttpStatusCode.OK);
    }

    private static ChatRequest CreateRequest()
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://api.deepseek.com/anthropic",
            ModelProviderId = (short)DBModelProvider.DeepSeek,
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
            DeploymentName = "deepseek-reasoner",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowStreaming = true,
            MaxResponseTokens = 2048,
            ApiTypeId = (byte)DBApiType.AnthropicMessages,
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
        };

        return new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8",
        };
    }

    [Fact]
    public async Task ChatStreamed_MessageDeltaWithoutInputTokens_PreservesPreviousInputTokens()
    {
        IHttpClientFactory httpClientFactory = CreateMockHttpClientFactory(
            "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"deepseek-reasoner\",\"content\":[],\"stop_reason\":null,\"stop_sequence\":null,\"usage\":{\"input_tokens\":36,\"output_tokens\":0}}}\n\n",
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":151}}\n\n",
            "data: {\"type\":\"message_stop\"}\n\n"
        );
        DeepSeekAnthropicService service = new(httpClientFactory);
        ChatRequest request = CreateRequest();

        List<ChatSegment> segments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        List<UsageChatSegment> usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.Equal(2, usageSegments.Count);
        Assert.Equal(36, usageSegments[0].Usage.InputTokens);
        Assert.Equal(0, usageSegments[0].Usage.OutputTokens);
        Assert.Equal(36, usageSegments[1].Usage.InputTokens);
        Assert.Equal(151, usageSegments[1].Usage.OutputTokens);

        FinishReasonChatSegment? finishReason = segments.OfType<FinishReasonChatSegment>().LastOrDefault();
        Assert.NotNull(finishReason);
        Assert.Equal(DBFinishReason.Success, finishReason.FinishReason);
    }

    [Fact]
    public async Task ChatStreamed_MessageDeltaWithoutCacheTokens_PreservesPreviousCacheTokens()
    {
        IHttpClientFactory httpClientFactory = CreateMockHttpClientFactory(
            "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"deepseek-reasoner\",\"content\":[],\"stop_reason\":null,\"stop_sequence\":null,\"usage\":{\"input_tokens\":36,\"cache_creation_input_tokens\":9,\"cache_read_input_tokens\":7,\"output_tokens\":0}}}\n\n",
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":151}}\n\n",
            "data: {\"type\":\"message_stop\"}\n\n"
        );
        DeepSeekAnthropicService service = new(httpClientFactory);
        ChatRequest request = CreateRequest();

        List<UsageChatSegment> usageSegments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            if (segment is UsageChatSegment usage)
            {
                usageSegments.Add(usage);
            }
        }

        Assert.Equal(2, usageSegments.Count);
        UsageChatSegment finalUsage = usageSegments[1];
        Assert.Equal(43, finalUsage.Usage.InputTokens);
        Assert.Equal(36, finalUsage.Usage.InputFreshTokens);
        Assert.Equal(151, finalUsage.Usage.OutputTokens);
        Assert.Equal(7, finalUsage.Usage.CacheTokens);
        Assert.Equal(9, finalUsage.Usage.CacheCreationTokens);
    }

    [Fact]
    public async Task ChatStreamed_MessageStartWithCacheReadTokens_TreatsInputTokensAsTotalPromptTokens()
    {
        IHttpClientFactory httpClientFactory = CreateMockHttpClientFactory(
            "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"deepseek-reasoner\",\"content\":[],\"stop_reason\":null,\"stop_sequence\":null,\"usage\":{\"input_tokens\":36,\"cache_creation_input_tokens\":9,\"cache_read_input_tokens\":7,\"output_tokens\":0}}}\n\n",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":151}}\n\n",
            "data: {\"type\":\"message_stop\"}\n\n"
        );
        DeepSeekAnthropicService service = new(httpClientFactory);
        ChatRequest request = CreateRequest();

        List<UsageChatSegment> usageSegments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            if (segment is UsageChatSegment usage)
            {
                usageSegments.Add(usage);
            }
        }

        Assert.Equal(2, usageSegments.Count);
        Assert.Equal(43, usageSegments[0].Usage.InputTokens);
        Assert.Equal(43, usageSegments[1].Usage.InputTokens);
        Assert.Equal(36, usageSegments[1].Usage.InputFreshTokens);
        Assert.Equal(7, usageSegments[1].Usage.CacheTokens);
        Assert.Equal(9, usageSegments[1].Usage.CacheCreationTokens);
    }

    [Fact]
    public void BuildRequestBody_SearchEnabled_AddsDeepSeekHostedWebSearchTool()
    {
        DeepSeekAnthropicService service = new(CreateMockHttpClientFactory());
        ChatRequest request = CreateRequest();
        request.ChatConfig.Model.CurrentSnapshot.AllowSearch = true;
        request.ChatConfig.WebSearchEnabled = true;

        MethodInfo method = typeof(AnthropicChatService).GetMethod("BuildRequestBody", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildRequestBody method not found.");
        JsonObject body = Assert.IsType<JsonObject>(method.Invoke(service, [request]));

        JsonObject tool = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(body["tools"])));
        Assert.Equal("web_search_20250305", (string?)tool["type"]);
        Assert.Equal("web_search", (string?)tool["name"]);
    }

    [Fact]
    public void BuildRequestBody_ClientHostedWebSearchTool_PreservesAllDefinitionFields()
    {
        DeepSeekAnthropicService service = new(CreateMockHttpClientFactory());
        ChatRequest request = CreateRequest();
        request.Tools.Add(new AnthropicBuiltInTool
        {
            Name = "web_search",
            Type = "web_search_20250305",
            Definition = new JsonObject
            {
                ["type"] = "web_search_20250305",
                ["name"] = "web_search",
                ["max_uses"] = 3,
                ["allowed_domains"] = new JsonArray("api-docs.deepseek.com"),
            },
        });

        MethodInfo method = typeof(AnthropicChatService).GetMethod("BuildRequestBody", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildRequestBody method not found.");
        JsonObject body = Assert.IsType<JsonObject>(method.Invoke(service, [request]));
        JsonObject tool = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(body["tools"])));

        Assert.Equal(3, (int?)tool["max_uses"]);
        Assert.Equal("api-docs.deepseek.com", (string?)tool["allowed_domains"]?[0]);
    }

    [Fact]
    public void BuildRequestBody_EmptyThinkingWithSignature_DropsUnsupportedRedactedThinkingBlock()
    {
        DeepSeekAnthropicService service = new(CreateMockHttpClientFactory());
        ChatRequest request = CreateRequest() with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralThinkContent.Create("", "9c976d06-9de1-4a07-a0b0-1c48e8b3b4f3"),
                    NeutralTextContent.Create("会话创建成功。"),
                    NeutralToolCallContent.Create("call_1", "write_file", "{}")),
                NeutralMessage.FromTool(
                    NeutralToolCallResponseContent.Create("call_1", "Wrote file")),
            ]
        };

        MethodInfo method = typeof(AnthropicChatService).GetMethod("BuildRequestBody", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildRequestBody method not found.");
        JsonObject body = Assert.IsType<JsonObject>(method.Invoke(service, [request]));
        string json = body.ToJsonString();
        JsonArray messages = Assert.IsType<JsonArray>(body["messages"]);
        JsonArray assistantContent = Assert.IsType<JsonArray>(messages[0]!["content"]);

        Assert.DoesNotContain("redacted_thinking", json);
        Assert.DoesNotContain("9c976d06-9de1-4a07-a0b0-1c48e8b3b4f3", json);
        Assert.Equal(["text", "tool_use"], assistantContent.Select(x => (string?)x!["type"]));
        Assert.Equal("会话创建成功。", (string?)assistantContent[0]?["text"]);
        Assert.Equal("write_file", (string?)assistantContent[1]?["name"]);
    }

    [Fact]
    public async Task ChatStreamed_HostedWebSearch_PreservesRawBlocksForStorage()
    {
        IHttpClientFactory factory = CreateMockHttpClientFactory(
            "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":10,\"output_tokens\":0}}}\n\n",
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"server_tool_use\",\"id\":\"srv_1\",\"name\":\"web_search\",\"input\":{},\"caller\":{\"type\":\"direct\"}}}\n\n",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"query\\\":\\\"DeepSeek\\\"}\"}}\n\n",
            "data: {\"type\":\"content_block_stop\",\"index\":0}\n\n",
            "data: {\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"web_search_tool_result\",\"tool_use_id\":\"srv_1\",\"content\":[{\"type\":\"web_search_result\",\"title\":\"DeepSeek\",\"url\":\"https://api-docs.deepseek.com\",\"encrypted_content\":\"opaque-cache-data\",\"page_age\":\"today\"}]}}\n\n",
            "data: {\"type\":\"content_block_stop\",\"index\":1}\n\n",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":20}}\n\n",
            "data: {\"type\":\"message_stop\"}\n\n");
        DeepSeekAnthropicService service = new(factory);
        List<ChatSegment> segments = [];

        await foreach (ChatSegment segment in service.ChatStreamed(CreateRequest(), CancellationToken.None))
        {
            segments.Add(segment);
        }

        ToolCallSegment call = Assert.Single(segments.OfType<ToolCallSegment>());
        Assert.Equal("web_search_call", call.Name);
        using JsonDocument callJson = JsonDocument.Parse(call.Arguments!);
        Assert.Equal("server_tool_use", callJson.RootElement.GetProperty("type").GetString());
        Assert.Equal("direct", callJson.RootElement.GetProperty("caller").GetProperty("type").GetString());
        Assert.Equal("DeepSeek", callJson.RootElement.GetProperty("input").GetProperty("query").GetString());

        ToolCallResponseSegment response = Assert.Single(segments.OfType<ToolCallResponseSegment>());
        using JsonDocument responseJson = JsonDocument.Parse(response.Response!);
        Assert.Equal("web_search_tool_result", responseJson.RootElement.GetProperty("type").GetString());
        Assert.Equal("opaque-cache-data", responseJson.RootElement.GetProperty("content")[0].GetProperty("encrypted_content").GetString());
    }

    [Fact]
    public void ConvertMessages_DeepSeekHostedWebSearch_RebuildsNativeAssistantBlocksInOrder()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConvertMessages method not found.");
        const string rawCall = "{\"type\":\"server_tool_use\",\"id\":\"srv_1\",\"name\":\"web_search\",\"input\":{\"query\":\"DeepSeek\"},\"caller\":{\"type\":\"direct\"}}";
        const string rawResult = "{\"type\":\"web_search_tool_result\",\"tool_use_id\":\"srv_1\",\"content\":[{\"type\":\"web_search_result\",\"title\":\"DeepSeek\",\"url\":\"https://api-docs.deepseek.com\",\"encrypted_content\":\"opaque-cache-data\"}]}";
        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("first thought", "sig-1"),
                NeutralToolCallContent.Create("srv_1", "web_search_call", rawCall),
                NeutralToolCallResponseContent.Create("srv_1", rawResult),
                NeutralThinkContent.Create("second thought", "sig-2"),
                NeutralTextContent.Create("answer"))
        ];

        JsonArray converted = Assert.IsType<JsonArray>(method.Invoke(null, [messages, true, UsageSource.Api, true]));
        JsonArray content = Assert.IsType<JsonArray>(converted[0]!["content"]);

        Assert.Equal(
            ["thinking", "server_tool_use", "web_search_tool_result", "thinking", "text"],
            content.Select(x => x!["type"]!.GetValue<string>()).ToArray());
        Assert.Equal("direct", (string?)content[1]?["caller"]?["type"]);
        Assert.Equal("opaque-cache-data", (string?)content[2]?["content"]?[0]?["encrypted_content"]);
        Assert.Equal("sig-1", (string?)content[0]?["signature"]);
        Assert.Equal("sig-2", (string?)content[3]?["signature"]);
    }

    [Fact]
    public void AnthropicResponse_DeepSeekHostedWebSearch_PreservesNativeBlocks()
    {
        const string rawCall = "{\"type\":\"server_tool_use\",\"id\":\"srv_1\",\"name\":\"web_search\",\"input\":{\"query\":\"DeepSeek\"},\"caller\":{\"type\":\"direct\"}}";
        const string rawResult = "{\"type\":\"web_search_tool_result\",\"tool_use_id\":\"srv_1\",\"content\":[{\"type\":\"web_search_result\",\"title\":\"DeepSeek\",\"url\":\"https://api-docs.deepseek.com\",\"encrypted_content\":\"opaque-cache-data\"}]}";
        ChatCompletionSnapshot snapshot = new()
        {
            Segments =
            [
                new ToolCallSegment { Index = 0, Id = "srv_1", Name = "web_search_call", Arguments = rawCall },
                ChatSegment.FromToolCallResponse("srv_1", rawResult, 0, true),
            ],
            Usage = ChatTokenUsage.Zero,
            IsUsageReliable = true,
            FinishReason = DBFinishReason.Success,
        };

        var response = snapshot.ToAnthropicResponse("deepseek-v4-flash", "msg_1");

        Assert.Equal(["server_tool_use", "web_search_tool_result"], response.Content.Select(x => x.Type));
        Assert.Equal("direct", response.Content[0].Caller?["type"]?.GetValue<string>());
        Assert.Equal("opaque-cache-data", response.Content[1].Content?[0]?["encrypted_content"]?.GetValue<string>());
    }

    [Fact]
    public void AnthropicStreamEvent_DeepSeekHostedWebSearch_PreservesNativeResultContent()
    {
        JsonNode content = JsonNode.Parse("[{\"type\":\"web_search_result\",\"encrypted_content\":\"opaque-cache-data\"}]")!;
        ContentBlockStartEvent streamEvent = new()
        {
            Index = 2,
            ContentBlock = new ContentBlockStartData
            {
                Type = "web_search_tool_result",
                ToolUseId = "srv_1",
                Content = content,
            },
        };

        string json = JsonSerializer.Serialize<AnthropicStreamEvent>(streamEvent);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement block = document.RootElement.GetProperty("content_block");

        Assert.Equal("web_search_tool_result", block.GetProperty("type").GetString());
        Assert.Equal("srv_1", block.GetProperty("tool_use_id").GetString());
        Assert.Equal("opaque-cache-data", block.GetProperty("content")[0].GetProperty("encrypted_content").GetString());
    }

    [Fact]
    public void Presentation_DeepSeekHostedWebSearch_RemovesOnlyEncryptedContent()
    {
        const string rawCall = "{\"type\":\"server_tool_use\",\"id\":\"srv_1\",\"name\":\"web_search\",\"input\":{\"query\":\"DeepSeek\"},\"caller\":{\"type\":\"direct\"}}";
        const string rawResult = "{\"type\":\"web_search_tool_result\",\"tool_use_id\":\"srv_1\",\"content\":[{\"type\":\"web_search_result\",\"title\":\"DeepSeek\",\"url\":\"https://api-docs.deepseek.com\",\"encrypted_content\":\"opaque-cache-data\",\"page_age\":\"today\"}]}";

        Assert.True(DeepSeekHostedWebSearch.TryCreatePresentationCall(rawCall, out string presentationCall));
        using JsonDocument call = JsonDocument.Parse(presentationCall);
        Assert.Equal("web_search_call", call.RootElement.GetProperty("type").GetString());
        Assert.Equal("DeepSeek", call.RootElement.GetProperty("action").GetProperty("query").GetString());

        Assert.True(DeepSeekHostedWebSearch.TryCreatePresentationResponse(rawResult, out string presentationResponse));
        using JsonDocument result = JsonDocument.Parse(presentationResponse);
        JsonElement item = Assert.Single(result.RootElement.EnumerateArray());
        Assert.False(item.TryGetProperty("encrypted_content", out _));
        Assert.Equal("today", item.GetProperty("page_age").GetString());
        Assert.Equal("https://api-docs.deepseek.com", item.GetProperty("url").GetString());
    }

    [Fact]
    public void Presentation_ResponsesWebSearch_RemainsUntouched()
    {
        const string responsesCall = "{\"type\":\"web_search_call\",\"status\":\"completed\",\"action\":{\"type\":\"search\",\"queries\":[\"GitHub Copilot CLI open source\"]}}";
        const string responsesResult = "[{\"type\":\"web_search_result\",\"title\":\"GitHub Copilot CLI\",\"url\":\"https://github.com/github/copilot-cli\"}]";

        Assert.False(DeepSeekHostedWebSearch.TryCreatePresentationCall(responsesCall, out string callPresentation));
        Assert.Equal(responsesCall, callPresentation);
        Assert.False(DeepSeekHostedWebSearch.TryCreatePresentationResponse(responsesResult, out string responsePresentation));
        Assert.Equal(responsesResult, responsePresentation);
    }
}
