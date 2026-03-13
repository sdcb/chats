using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class NovitaChatServiceTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestNovitaChatService(IHttpClientFactory httpClientFactory) : NovitaChatService(httpClientFactory)
    {
        public JsonObject ToUpstreamMessage(NeutralMessage message) => base.ToOpenAIMessage(message);
        public JsonObject ToUpstreamRequestBody(ChatRequest request, bool stream) => base.BuildRequestBody(request, stream);
    }

    private static ChatRequest CreateBaseChatRequest()
    {
        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://api.novita.ai/openai",
            ModelProviderId = (short)DBModelProvider.Novita,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "deepseek/deepseek-v3.2",
            ModelKeyId = 1,
            ModelKey = modelKey,
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
        };

        var chatConfig = new ChatConfig
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
    public void AssistantToolCall_WithThinking_ShouldIncludeReasoningContent()
    {
        var service = new TestNovitaChatService(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("t1"),
            NeutralToolCallContent.Create("call_1", "get_weather", "{\"location\":\"SF\"}"));

        JsonObject upstream = service.ToUpstreamMessage(msg);

        Assert.Equal("assistant", (string?)upstream["role"]);
        Assert.NotNull(upstream["tool_calls"]);
        Assert.True(upstream.ContainsKey("reasoning_content"));
        Assert.Equal("t1", (string?)upstream["reasoning_content"]);
    }

    [Fact]
    public void AssistantWithoutToolCall_WithThinking_ShouldNotIncludeReasoningContent()
    {
        var service = new TestNovitaChatService(new DummyHttpClientFactory());

        NeutralMessage msg = NeutralMessage.FromAssistant(
            NeutralThinkContent.Create("t1"),
            NeutralTextContent.Create("hello"));

        JsonObject upstream = service.ToUpstreamMessage(msg);

        Assert.False(upstream.ContainsKey("reasoning_content"));
    }
}
