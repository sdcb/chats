using System.Net;
using System.Net.Http;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class QiniuChatServiceTests
{
    [Fact]
    public async Task ListModels_WhenUpstreamFails_ReturnsFallbackModel()
    {
        var service = new QiniuChatService(new FixedHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var modelKey = CreateModelKey();

        string[] models = await service.ListModels(modelKey, CancellationToken.None);

        Assert.Equal(["deepseek-v3"], models);
    }

    [Fact]
    public async Task ListModels_WhenUpstreamReturnsModels_UsesUpstreamResult()
    {
        var service = new QiniuChatService(
            new FixedHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"id":"qwen-max"},{"id":"deepseek-v3"}]}""")
            }));
        var modelKey = CreateModelKey();

        string[] models = await service.ListModels(modelKey, CancellationToken.None);

        Assert.Equal(["qwen-max", "deepseek-v3"], models);
    }

    private static ModelKey CreateModelKey() => new()
    {
        Id = 1,
        Name = "qiniu-key",
        Secret = "test-secret",
        Host = "https://api.qnaigc.com/v1",
        ModelProviderId = (short)DBModelProvider.Qiniu,
    };

    private sealed class FixedHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FixedHttpMessageHandler(responder));
    }

    private sealed class FixedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
