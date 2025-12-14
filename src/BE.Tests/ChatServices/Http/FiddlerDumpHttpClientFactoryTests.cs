using System.Net;
using System.Text;
using Chats.BE.Tests.ChatServices.Http;

namespace Chats.BE.Tests.ChatServices;

public class FiddlerDumpHttpClientFactoryTests
{
    private const string TestDataPath = "ChatServices/GoogleAI/FiddlerDump";

    [Fact]
    public async Task WhenRequestMatchesDump_ShouldReturnResponse()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var statusCode = (HttpStatusCode)dump.Response.StatusCode;

        IHttpClientFactory factory = new FiddlerDumpHttpClientFactory(dump.Response.Chunks, statusCode, dump.Request.Body);
        using HttpClient client = factory.CreateClient("test");

        using var request = new HttpRequestMessage(HttpMethod.Post, dump.Request.Url)
        {
            Content = new StringContent(dump.Request.Body, Encoding.UTF8, "application/json")
        };

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(statusCode, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("candidates", body);
    }

    [Fact]
    public async Task WhenRequestJsonValueMismatch_ShouldThrow()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);

        IHttpClientFactory factory = new FiddlerDumpHttpClientFactory(dump.Response.Chunks, (HttpStatusCode)dump.Response.StatusCode, dump.Request.Body);
        using HttpClient client = factory.CreateClient("test");

        string actualBody = dump.Request.Body.Replace("\"temperature\":1", "\"temperature\":2", StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Post, dump.Request.Url)
        {
            Content = new StringContent(actualBody, Encoding.UTF8, "application/json")
        };

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        // Assert
        Assert.Contains("Request JSON mismatch", ex.Message);
        Assert.Contains("temperature", ex.Message);
    }

    [Fact]
    public async Task WhenRequestJsonShapeMismatch_ShouldThrow()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);

        IHttpClientFactory factory = new FiddlerDumpHttpClientFactory(dump.Response.Chunks, (HttpStatusCode)dump.Response.StatusCode, dump.Request.Body);
        using HttpClient client = factory.CreateClient("test");

        using var request = new HttpRequestMessage(HttpMethod.Post, dump.Request.Url)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        // Assert
        Assert.Contains("missing property", ex.Message);
        Assert.Contains("$.model", ex.Message);
    }

    [Fact]
    public async Task WhenActualBodyIsInvalidJson_ShouldThrow()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);

        IHttpClientFactory factory = new FiddlerDumpHttpClientFactory(dump.Response.Chunks, (HttpStatusCode)dump.Response.StatusCode, dump.Request.Body);
        using HttpClient client = factory.CreateClient("test");

        using var request = new HttpRequestMessage(HttpMethod.Post, dump.Request.Url)
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        // Assert
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task WhenExpectedBodyIsInvalidJson_ShouldThrow()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);

        IHttpClientFactory factory = new FiddlerDumpHttpClientFactory(dump.Response.Chunks, (HttpStatusCode)dump.Response.StatusCode, expectedRequestBody: "not-json");
        using HttpClient client = factory.CreateClient("test");

        using var request = new HttpRequestMessage(HttpMethod.Post, dump.Request.Url)
        {
            Content = new StringContent(dump.Request.Body, Encoding.UTF8, "application/json")
        };

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        // Assert
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task WhenJsonStringIsEscapedOrUnescaped_ShouldBeEqual()
    {
        // Arrange
        const string expectedJson = "{\"msg\":\"\\u4F60\\u597D\"}"; // "你好" in \u escapes
        const string actualJson = "{\"msg\":\"你好\"}";

        IHttpClientFactory factory = new FiddlerDumpHttpClientFactory(
            chunks: ["{\"ok\":true}"],
            statusCode: HttpStatusCode.OK,
            expectedRequestBody: expectedJson);

        using HttpClient client = factory.CreateClient("test");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/")
        {
            Content = new StringContent(actualJson, Encoding.UTF8, "application/json")
        };

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"ok\":true", body);
    }
}
