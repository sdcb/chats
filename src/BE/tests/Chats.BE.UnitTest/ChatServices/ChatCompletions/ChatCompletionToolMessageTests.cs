using System.Text.Json.Nodes;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class ChatCompletionToolMessageTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestableChatCompletionService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
    {
        public JsonObject ToUpstreamMessage(NeutralMessage message) => base.ToOpenAIMessage(message);

        public JsonArray ToUpstreamMessages(ChatRequest request) => base.BuildMessages(request);
    }

    private static ChatRequest CreateBaseChatRequest(params NeutralMessage[] messages)
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://api.openai.com/v1",
            ModelProviderId = (short)DBModelProvider.OpenAI,
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
            DeploymentName = "gpt-4.1",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowVision = true,
            AllowToolCall = true,
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

        ChatConfig chatConfig = new()
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        return new ChatRequest
        {
            Messages = messages,
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
        };
    }

    [Fact]
    public void ToOpenAIMessage_ToolMessageWithImage_UsesMultipartContent()
    {
        TestableChatCompletionService service = new(new DummyHttpClientFactory());

        NeutralMessage message = NeutralMessage.FromTool(
            NeutralToolCallResponseContent.Create("call_1", "tool output"),
            NeutralFileUrlContent.Create("https://example.com/chart.png"));

        JsonObject upstream = service.ToUpstreamMessage(message);

        Assert.Equal("tool", (string?)upstream["role"]);
        Assert.Equal("call_1", (string?)upstream["tool_call_id"]);

        JsonArray parts = Assert.IsType<JsonArray>(upstream["content"]);
        Assert.Equal("text", (string?)parts[0]?["type"]);
        Assert.Equal("tool output", (string?)parts[0]?["text"]);
        Assert.Equal("image_url", (string?)parts[1]?["type"]);
        Assert.Equal("https://example.com/chart.png", (string?)parts[1]?["image_url"]?["url"]);
    }

    [Fact]
    public void BuildMessages_ToolMessageWithMultipleResponses_SplitsThem()
    {
        TestableChatCompletionService service = new(new DummyHttpClientFactory());
        ChatRequest request = CreateBaseChatRequest(
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "first result"),
                NeutralToolCallResponseContent.Create("call_2", "second result")));

        JsonArray upstreamMessages = service.ToUpstreamMessages(request);

        Assert.Collection(upstreamMessages,
            first =>
            {
                Assert.Equal("tool", (string?)first?["role"]);
                Assert.Equal("call_1", (string?)first?["tool_call_id"]);
                Assert.Equal("first result", (string?)first?["content"]);
            },
            second =>
            {
                Assert.Equal("tool", (string?)second?["role"]);
                Assert.Equal("call_2", (string?)second?["tool_call_id"]);
                Assert.Equal("second result", (string?)second?["content"]);
            });
    }

    [Fact]
    public void ToOpenAIMessage_AssistantToolCallWithEmptyParameters_UsesEmptyJsonObjectString()
    {
        TestableChatCompletionService service = new(new DummyHttpClientFactory());

        NeutralMessage message = NeutralMessage.FromAssistant(
            NeutralToolCallContent.Create("call_1", "create_docker_session", "")
        );

        JsonObject upstream = service.ToUpstreamMessage(message);

        JsonArray toolCalls = Assert.IsType<JsonArray>(upstream["tool_calls"]);
        Assert.Equal("{}", (string?)toolCalls[0]?["function"]?["arguments"]);
    }
}