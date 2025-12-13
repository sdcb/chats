using System.Net.Http;
using System.Text.Json.Nodes;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Tests.ChatServices.OpenAI;

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
    }

    [Fact]
    public void ToOpenAIMessage_AssistantToolCall_WithThinking_AttachesReasoningContent()
    {
        // Arrange
        var svc = new TestableDeepSeekChatService(new DummyHttpClientFactory());

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
        var svc = new TestableDeepSeekChatService(new DummyHttpClientFactory());

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
}
