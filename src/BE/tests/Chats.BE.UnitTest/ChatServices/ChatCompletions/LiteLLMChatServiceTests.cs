using Chats.BE.Services.Models.ChatServices.OpenAI;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class LiteLLMChatServiceTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [Fact]
    public void LiteLLMChatService_IsOpenAICompatible()
    {
        LiteLLMChatService svc = new(new DummyHttpClientFactory());

        // LiteLLM Proxy speaks the OpenAI Chat Completions wire format, so the
        // provider reuses the OpenAI-compatible transport and /models discovery.
        Assert.IsAssignableFrom<ChatCompletionService>(svc);
    }
}
