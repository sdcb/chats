using System.Net;
using System.Text;

namespace Chats.BE.UnitTest.ChatServices.Http;

public class ReplayHttpClientFactoryTests
{
    private const string ResponseBody = "{\"candidates\":[]}";
    private const string ExpectedRequestBody = "{\"model\":\"test\",\"temperature\":1}";

    [Fact]
    public async Task WhenRequestMatchesExpectedJson_ShouldReturnResponse()
    {
        IHttpClientFactory factory = new ReplayHttpClientFactory(ResponseBody, HttpStatusCode.OK, ExpectedRequestBody);
        using HttpClient client = factory.CreateClient("test");
        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent(ExpectedRequestBody, Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(ResponseBody, body);
    }

    [Fact]
    public async Task WhenRequestJsonValueMismatch_ShouldThrow()
    {
        IHttpClientFactory factory = new ReplayHttpClientFactory(ResponseBody, HttpStatusCode.OK, ExpectedRequestBody);
        using HttpClient client = factory.CreateClient("test");
        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent("{\"model\":\"test\",\"temperature\":2}", Encoding.UTF8, "application/json")
        };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        Assert.Contains("Request JSON mismatch", ex.Message);
        Assert.Contains("temperature", ex.Message);
    }

    [Fact]
    public async Task WhenRequestJsonShapeMismatch_ShouldThrow()
    {
        IHttpClientFactory factory = new ReplayHttpClientFactory(ResponseBody, HttpStatusCode.OK, ExpectedRequestBody);
        using HttpClient client = factory.CreateClient("test");
        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        Assert.Contains("missing property", ex.Message);
        Assert.Contains("$.model", ex.Message);
    }

    [Fact]
    public async Task WhenActualBodyIsInvalidJson_ShouldThrow()
    {
        IHttpClientFactory factory = new ReplayHttpClientFactory(ResponseBody, HttpStatusCode.OK, ExpectedRequestBody);
        using HttpClient client = factory.CreateClient("test");
        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task WhenExpectedBodyIsInvalidJson_ShouldThrow()
    {
        IHttpClientFactory factory = new ReplayHttpClientFactory(ResponseBody, HttpStatusCode.OK, expectedRequestBody: "not-json");
        using HttpClient client = factory.CreateClient("test");
        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent(ExpectedRequestBody, Encoding.UTF8, "application/json")
        };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task WhenJsonStringIsEscapedOrUnescaped_ShouldBeEqual()
    {
        const string expectedJson = "{\"msg\":\"\\u4F60\\u597D\"}";
        const string actualJson = "{\"msg\":\"你好\"}";
        IHttpClientFactory factory = new ReplayHttpClientFactory("{\"ok\":true}", HttpStatusCode.OK, expectedJson);
        using HttpClient client = factory.CreateClient("test");
        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent(actualJson, Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"ok\":true", body);
    }
}
