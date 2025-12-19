namespace Chats.BE.Tests.ChatServices;

public class FiddlerHttpDumpParserTests
{
    private const string TestDataPath = "ChatServices/GoogleAI/FiddlerDump";
    private const string OpenAITestDataPath = "ChatServices/OpenAI/FiddlerDump";

    [Fact]
    public void CanParseCodeExecuteDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.dump");
        
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
        Assert.NotEmpty(dump.Response.Body);
        
        // Dechunked body应该不包含chunk大小行
        Assert.DoesNotContain("2da", dump.Response.Body.Split('\n')[0]);
        Assert.Contains("candidates", dump.Response.Body);
    }

    [Fact]
    public void CanParseToolCallDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ToolCall.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Equal("POST", dump.Request.Method);
        Assert.Contains("run_code", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Contains("functionCall", dump.Response.Body);
    }

    [Fact]
    public void CanParseWebSearchDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "WebSearch.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Equal("POST", dump.Request.Method);
        Assert.Contains("googleSearch", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Contains("groundingMetadata", dump.Response.Body);
        Assert.Contains("webSearchQueries", dump.Response.Body);
    }

    [Fact]
    public void CanParseImageGenerateDump()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ImageGenerate.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Request
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-image:streamGenerateContent", dump.Request.Url);
        Assert.Contains("IMAGE", dump.Request.Body);
        
        // Assert - Response
        Assert.Equal(200, dump.Response.StatusCode);
        Assert.Contains("inlineData", dump.Response.Body);
    }

    [Fact]
    public void CanExtractRequestHeaders()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.dump");
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
        var filePath = Path.Combine(TestDataPath, "CodeExecute.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert
        Assert.True(dump.Response.Headers.TryGetValue("Content-Type", out var contentType));
        Assert.Contains("application/json", contentType);
        
        Assert.True(dump.Response.Headers.TryGetValue("Transfer-Encoding", out var encoding));
        Assert.Equal("chunked", encoding);
    }

    [Fact]
    public void ChunkedResponse_ShouldHaveCorrectChunkCount_CodeExecute()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Verify chunks are parsed correctly
        Assert.True(dump.Response.Chunks.Count > 0, $"Expected at least 1 chunk, got {dump.Response.Chunks.Count}");
        
        // Verify each chunk is valid JSON (part of a JSON array)
        Assert.StartsWith("[{", dump.Response.Chunks[0].TrimStart());
        
        // Verify the combined body is valid JSON
        Assert.Contains("candidates", dump.Response.Body);
    }

    [Fact]
    public void ChunkedResponse_ShouldHaveCorrectChunkCount_ThoughtSignature()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ThoughtSignature.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Debug: output chunk info
        var chunkInfo = string.Join(", ", dump.Response.Chunks.Select((c, i) => $"Chunk{i}:{c.Length}chars"));
        
        // Assert - Multiple chunks expected
        Assert.True(dump.Response.Chunks.Count > 1, $"Expected more than 1 chunk, got {dump.Response.Chunks.Count}. Chunks: [{chunkInfo}]");
        
        // Verify the combined body is valid JSON
        Assert.StartsWith("[{", dump.Response.Body.TrimStart());
        Assert.EndsWith("]", dump.Response.Body.TrimEnd());
    }

    [Fact]
    public void ChunkedResponse_XiaomiMimo_ShouldHaveCorrectChunks()
    {
        // Arrange
        var filePath = Path.Combine(OpenAITestDataPath, "XiaomiMimo-ToolCall.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - each chunk should start with "data: " (SSE format)
        Assert.True(dump.Response.Chunks.Count > 0);
        
        // First chunk should contain SSE data
        Assert.Contains("data:", dump.Response.Chunks[0]);
    }

    [Fact]
    public void NonChunkedResponse_ShouldHaveSingleChunk()
    {
        // Arrange
        var filePath = Path.Combine(OpenAITestDataPath, "XiaomiMimo-NonStream.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert - Non-chunked response should have exactly 1 chunk containing the entire body
        Assert.Single(dump.Response.Chunks);
        Assert.Contains("\"id\":", dump.Response.Chunks[0]);
        Assert.Contains("choices", dump.Response.Chunks[0]);
    }

    [Fact]
    public void ErrorResponse_ShouldParseCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "Error_429.dump");
        
        // Act
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        
        // Assert
        Assert.Equal(429, dump.Response.StatusCode);
        Assert.Contains("error", dump.Response.Body);
        Assert.Contains("RESOURCE_EXHAUSTED", dump.Response.Body);
    }
}
