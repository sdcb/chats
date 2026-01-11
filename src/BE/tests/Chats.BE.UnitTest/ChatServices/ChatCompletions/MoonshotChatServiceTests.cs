using System.Text.Json.Nodes;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class MoonshotChatServiceTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestableMoonshotChatService : MoonshotChatService
    {
        public TestableMoonshotChatService(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public JsonObject ToUpstreamMessage(NeutralMessage message) => base.ToOpenAIMessage(message);
    }

    [Fact]
    public void ToOpenAIMessage_AssistantToolCall_WithThinking_AttachesReasoningContent()
    {
        var svc = new TestableMoonshotChatService(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("thought-1"),
            NeutralToolCallContent.Create("call_1", "get_date", "{}")
        );

        JsonObject upstream = svc.ToUpstreamMessage(msg);

        Assert.Equal("assistant", (string?)upstream["role"]);
        Assert.NotNull(upstream["tool_calls"]);
        Assert.Equal("thought-1", (string?)upstream["reasoning_content"]);
    }

    [Fact]
    public void ToOpenAIMessage_AssistantNoToolCall_WithThinking_DoesNotAttachReasoningContent()
    {
        var svc = new TestableMoonshotChatService(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("thought-1"),
            NeutralTextContent.Create("final")
        );

        JsonObject upstream = svc.ToUpstreamMessage(msg);

        Assert.Equal("assistant", (string?)upstream["role"]);
        Assert.Null(upstream["tool_calls"]);
        Assert.Null(upstream["reasoning_content"]);
    }
}
