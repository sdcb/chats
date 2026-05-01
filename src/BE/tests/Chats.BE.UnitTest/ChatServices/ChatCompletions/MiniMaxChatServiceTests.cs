using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class MiniMaxChatServiceTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestMiniMaxChatService(IHttpClientFactory httpClientFactory) : MiniMaxChatService(httpClientFactory)
    {
        public JsonObject ToUpstreamMessage(NeutralMessage message) => base.ToOpenAIMessage(message);
        public JsonObject ToUpstreamRequestBody(ChatRequest request, bool stream) => base.BuildRequestBody(request, stream);
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
            Host = "https://api.minimax.chat/v1",
            ModelProviderId = (short)DBModelProvider.MiniMax,
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
            DeploymentName = "MiniMax-M2",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowSearch = false,
            AllowVision = false,
            AllowStreaming = true,
            AllowCodeExecution = false,
            AllowToolCall = true,
            ContextWindow = 128000,
            MaxResponseTokens = 8192,
            MinTemperature = 0,
            MaxTemperature = 2,
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
            Messages = [NeutralMessage.FromUserText("hi")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
        };
    }

    [Fact]
    public void AssistantToolCall_WithThinking_ShouldIncludeReasoningDetailsArray()
    {
        TestMiniMaxChatService service = new(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("t1"),
            NeutralToolCallContent.Create("call_1", "get_weather", "{\"location\":\"SF\"}"));

        JsonObject upstream = service.ToUpstreamMessage(msg);

        Assert.Equal("assistant", (string?)upstream["role"]);
        Assert.NotNull(upstream["tool_calls"]);
        Assert.NotNull(upstream["content"]);

        JsonArray? reasoningDetails = upstream["reasoning_details"] as JsonArray;
        Assert.NotNull(reasoningDetails);
        Assert.True(reasoningDetails!.Count > 0);
        Assert.Equal("t1", (string?)reasoningDetails[0]?["text"]);
    }

    [Fact]
    public void AssistantWithoutToolCall_WithThinking_ShouldNotIncludeReasoningDetails()
    {
        TestMiniMaxChatService service = new(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("t1"),
            NeutralTextContent.Create("hello"));

        JsonObject upstream = service.ToUpstreamMessage(msg);

        Assert.Null(upstream["reasoning_details"]);
    }

    [Fact]
    public void BuildRequestBody_ShouldIncludeReasoningSplitTrue()
    {
        TestMiniMaxChatService service = new(new DummyHttpClientFactory());
        ChatRequest req = CreateBaseChatRequest();

        JsonObject body = service.ToUpstreamRequestBody(req, stream: true);
        Assert.True((bool?)body["reasoning_split"]);
    }
}
