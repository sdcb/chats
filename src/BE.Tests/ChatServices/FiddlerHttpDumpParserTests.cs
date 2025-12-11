namespace Chats.BE.Tests.ChatServices;

public class FiddlerHttpDumpParserTests
{
    private const string TestDataPath = "ChatServices/GoogleAI";

    [Fact]
    public void CanParseCodeExecuteDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Equal("POST", dump.Request.Method);
        Assert.Contains("gemini-2.5-flash:streamGenerateContent", dump.Request.Url);
        Assert.Equal("HTTP/1.1", dump.Request.HttpVersion);
        Assert.NotEmpty(dump.Request.Headers);
        Assert.Contains("x-goog-api-key", dump.Request.Headers.Keys);
        Assert.NotEmpty(dump.Request.Body);
        Assert.Contains("\"model\"", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Equal("OK", dump.Response.StatusText);
        Assert.NotEmpty(dump.Response.Headers);
        Assert.True(dump.Response.Headers.ContainsKey("Transfer-Encoding"));
        Assert.NotEmpty(dump.Response.RawBody);
        Assert.NotEmpty(dump.Response.DechunkedBody);
        
        // Dechunked body应该不包含chunk大小行
        Assert.DoesNotContain("2da", dump.Response.DechunkedBody.Split('\n')[0]);
        Assert.Contains("candidates", dump.Response.DechunkedBody);
    }

    [Fact]
    public void CanParseToolCallDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ToolCall.txt");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Equal("POST", dump.Request.Method);
        Assert.Contains("run_code", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Contains("functionCall", dump.Response.DechunkedBody);
    }

    [Fact]
    public void CanParseWebSearchDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "WebSearch.txt");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Equal("POST", dump.Request.Method);
        Assert.Contains("googleSearch", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Contains("groundingMetadata", dump.Response.DechunkedBody);
        Assert.Contains("webSearchQueries", dump.Response.DechunkedBody);
    }

    [Fact]
    public void CanParseImageGenerateDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ImageGenerate.txt");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Contains("image-generation", dump.Request.Url);
        Assert.Contains("IMAGE", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Contains("inlineData", dump.Response.DechunkedBody);
    }

    [Fact]
    public void CanExtractRequestHeaders()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert
        Assert.True(dump.Request.Headers.TryGetValue("Content-Type", out var contentType));
        Assert.Equal("application/json", contentType);
        
        Assert.True(dump.Request.Headers.TryGetValue("Host", out var host));
        Assert.Equal("generativelanguage.googleapis.com", host);
    }

    [Fact]
    public void CanExtractResponseHeaders()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert
        Assert.True(dump.Response.Headers.TryGetValue("Content-Type", out var contentType));
        Assert.Contains("application/json", contentType);
        
        Assert.True(dump.Response.Headers.TryGetValue("Transfer-Encoding", out var encoding));
        Assert.Equal("chunked", encoding);
    }
}
