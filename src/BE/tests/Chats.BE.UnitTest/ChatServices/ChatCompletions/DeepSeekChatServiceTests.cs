using System.Text.Json.Nodes;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class DeepSeekChatServiceTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestableDeepSeekChatService : DeepSeekChatService
    {
        public TestableDeepSeekChatService(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

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
            Host = "https://api.deepseek.com",
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
            AllowVision = false,
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
    public void ToOpenAIMessage_AssistantToolCall_WithThinking_AttachesReasoningContent()
    {
        // Arrange
        TestableDeepSeekChatService svc = new(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("thought-1"),
            NeutralToolCallContent.Create("call_1", "get_date", "{}")
        );

        // Act
        JsonObject upstream = svc.ToUpstreamMessage(msg);

        // Assert
        Assert.Equal("assistant", (string?)upstream["role"]);
        Assert.NotNull(upstream["tool_calls"]);
        Assert.Equal("thought-1", (string?)upstream["reasoning_content"]);
    }

    [Fact]
    public void ToOpenAIMessage_AssistantNoToolCall_WithThinking_DoesNotAttachReasoningContent()
    {
        // Arrange
        TestableDeepSeekChatService svc = new(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("thought-1"),
            NeutralTextContent.Create("final")
        );

        // Act
        JsonObject upstream = svc.ToUpstreamMessage(msg);

        // Assert
        Assert.Equal("assistant", (string?)upstream["role"]);
        Assert.Null(upstream["tool_calls"]);
        Assert.Null(upstream["reasoning_content"]);
    }

    [Fact]
    public void ToOpenAIMessage_ToolMessageWithTextOnlyParts_CollapsesContentToString()
    {
        TestableDeepSeekChatService svc = new(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromTool(
            NeutralToolCallResponseContent.Create("call_1", "exit code: 0"),
            NeutralTextContent.Create("https://example.com/chart.png")
        );

        JsonObject upstream = svc.ToUpstreamMessage(msg);

        Assert.Equal("tool", (string?)upstream["role"]);
        Assert.Equal("call_1", (string?)upstream["tool_call_id"]);
        Assert.Equal("exit code: 0\nhttps://example.com/chart.png", (string?)upstream["content"]);
    }

    [Fact]
    public void BuildMessages_ToolMessageWithTextOnlyParts_CollapsesContentToString()
    {
        TestableDeepSeekChatService svc = new(new DummyHttpClientFactory());
        ChatRequest request = CreateBaseChatRequest(
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "exit code: 0"),
                NeutralTextContent.Create("https://example.com/chart.png")
            )
        );

        JsonArray upstreamMessages = svc.ToUpstreamMessages(request);
        JsonObject toolMessage = Assert.IsType<JsonObject>(upstreamMessages[0]);

        Assert.Equal("tool", (string?)toolMessage["role"]);
        Assert.Equal("exit code: 0\nhttps://example.com/chart.png", (string?)toolMessage["content"]);
    }
}
