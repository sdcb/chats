using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.GoogleAI;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Net;
using System.Text;

namespace Chats.BE.Tests.ChatServices.GoogleAI;

public class GoogleAI2ChatServiceTest
{
    private const string TestDataPath = "ChatServices/GoogleAI/FiddlerDump";

    /// <summary>
    /// 基于 Fiddler dump 文件创建模拟的 HttpClientFactory
    /// </summary>
    private static IHttpClientFactory CreateMockHttpClientFactory(FiddlerHttpDumpParser.HttpDump dump)
    {
        var statusCode = (HttpStatusCode)dump.Response.StatusCode;
        return new FakeHttpClientFactory(dump.Response.Chunks, statusCode);
    }

    private static ChatRequest CreateBaseChatRequest(string modelDeploymentName, string prompt, Action<ChatConfig>? configure = null)
    {
        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = null,
            ModelProviderId = 1,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = modelDeploymentName,
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowSearch = true,
            AllowVision = true,
            AllowStreaming = true,
            AllowCodeExecution = true,
            AllowToolCall = true,
            ContextWindow = 128000,
            MaxResponseTokens = 8192,
            MinTemperature = 0,
            MaxTemperature = 2,
        };

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
            Temperature = 1.0f,
        };

        configure?.Invoke(chatConfig);

        return new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText(prompt)],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
        };
    }

    [Fact]
    public async Task CodeExecute_ShouldReturnCodeExecutionResult()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var chatCompletionService = new ChatCompletionService(httpClientFactory);
        var service = new GoogleAI2ChatService(chatCompletionService, httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.5-flash", "调用内置工具，计算1234/5432=?", cfg =>
        {
            cfg.CodeExecutionEnabled = true;
        });

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);
        
        // 应该有思考内容（thought）
        var thinkSegments = segments.OfType<ThinkChatSegment>().ToList();
        Assert.NotEmpty(thinkSegments);
        
        // 应该有代码执行工具调用
        var toolCallSegments = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCallSegments);
        var codeExecCall = toolCallSegments.First();
        Assert.Equal("PYTHON", codeExecCall.Name);
        Assert.Contains("1234", codeExecCall.Arguments);
        Assert.Contains("5432", codeExecCall.Arguments);
        
        // 应该有代码执行结果
        var toolResponseSegments = segments.OfType<ToolCallResponseSegment>().ToList();
        Assert.NotEmpty(toolResponseSegments);
        Assert.True(toolResponseSegments.First().IsSuccess);
        Assert.Contains("0.227", toolResponseSegments.First().Response);
        
        // 应该有文本输出
        var textSegments = segments.OfType<TextChatSegment>().ToList();
        Assert.NotEmpty(textSegments);
        
        // 应该有usage信息
        var usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
        Assert.True(usageSegments.Last().Usage.InputTokens > 0);
        
        // 应该有finish reason
        var finishSegments = segments.OfType<FinishReasonChatSegment>().ToList();
        Assert.NotEmpty(finishSegments);
        Assert.Equal(DBFinishReason.Success, finishSegments.Last().FinishReason);
    }

    [Fact]
    public async Task ToolCall_ShouldReturnFunctionCall()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ToolCall.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var chatCompletionService = new ChatCompletionService(httpClientFactory);
        var service = new GoogleAI2ChatService(chatCompletionService, httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.5-flash", "调用C#工具，计算1234/5432=?", cfg =>
        {
            // 添加工具定义
        });
        request = request with
        {
            Tools =
            [
                new FunctionTool
                {
                    FunctionName = "run_code",
                    FunctionDescription = "执行C#代码",
                    FunctionParameters = """{"type":"object","properties":{"code":{"type":"string"}},"required":["code"]}"""
                }
            ]
        };

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);
        
        // 应该有函数调用
        var toolCallSegments = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCallSegments);
        var functionCall = toolCallSegments.First();
        Assert.Equal("run_code", functionCall.Name);
        Assert.Contains("1234", functionCall.Arguments);
        Assert.Contains("5432", functionCall.Arguments);
        
        // 应该有usage信息
        var usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
    }

    [Fact]
    public async Task WebSearch_ShouldReturnSearchResults()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "WebSearch.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var chatCompletionService = new ChatCompletionService(httpClientFactory);
        var service = new GoogleAI2ChatService(chatCompletionService, httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.5-flash", "今天有什么新闻？", cfg =>
        {
            cfg.WebSearchEnabled = true;
        });

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);
        
        // 应该有思考内容
        var thinkSegments = segments.OfType<ThinkChatSegment>().ToList();
        Assert.NotEmpty(thinkSegments);
        
        // 应该有文本输出
        var textSegments = segments.OfType<TextChatSegment>().ToList();
        Assert.NotEmpty(textSegments);
        
        // 应该有usage信息
        var usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
        
        // 应该有finish reason
        var finishSegments = segments.OfType<FinishReasonChatSegment>().ToList();
        Assert.NotEmpty(finishSegments);
        Assert.Equal(DBFinishReason.Success, finishSegments.Last().FinishReason);
    }

    [Fact]
    public async Task ImageGenerate_ShouldReturnImage()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ImageGenerate.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var chatCompletionService = new ChatCompletionService(httpClientFactory);
        var service = new GoogleAI2ChatService(chatCompletionService, httpClientFactory);

        // 使用支持图片生成的模型名称
        var request = CreateBaseChatRequest("gemini-2.0-flash-exp-image-generation", "1+1=?");

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);
        
        // 应该有文本输出
        var textSegments = segments.OfType<TextChatSegment>().ToList();
        Assert.NotEmpty(textSegments);
        
        // 应该有图片输出
        var imageSegments = segments.OfType<ImageChatSegment>().ToList();
        Assert.NotEmpty(imageSegments);
        var image = imageSegments.First() as Base64Image;
        Assert.NotNull(image);
        Assert.Equal("image/png", image.ContentType);
        Assert.NotEmpty(image.Base64);
        
        // 应该有usage信息
        var usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
    }

    [Fact]
    public async Task Error404_ShouldThrowRawChatServiceException()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "Error_404.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var chatCompletionService = new ChatCompletionService(httpClientFactory);
        var service = new GoogleAI2ChatService(chatCompletionService, httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.0-flash-exp-image-generation", "生成一张小猫的图片");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RawChatServiceException>(async () =>
        {
            await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
            {
                // 消费流
            }
        });

        Assert.Equal(404, exception.StatusCode);
        Assert.Contains("NOT_FOUND", exception.Body);
        Assert.Contains("is not found for API version", exception.Body);
    }

    [Fact]
    public async Task Error429_ShouldThrowRawChatServiceException()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "Error_429.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var chatCompletionService = new ChatCompletionService(httpClientFactory);
        var service = new GoogleAI2ChatService(chatCompletionService, httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.0-flash-exp", "生成一张小猫的图片");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RawChatServiceException>(async () =>
        {
            await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
            {
                // 消费流
            }
        });

        Assert.Equal(429, exception.StatusCode);
        Assert.Contains("RESOURCE_EXHAUSTED", exception.Body);
        Assert.Contains("exceeded your current quota", exception.Body);
    }
}

/// <summary>
/// 模拟的 HttpClientFactory，基于 Fiddler dump 的 chunks 逐块返回响应
/// </summary>
public class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly List<string> _chunks;
    private readonly HttpStatusCode _statusCode;

    public FakeHttpClientFactory(List<string> chunks, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _chunks = chunks;
        _statusCode = statusCode;
    }

    public HttpClient CreateClient(string name)
    {
        var handler = new FakeHttpMessageHandler(_chunks, _statusCode);
        return new HttpClient(handler);
    }
}

/// <summary>
/// 模拟的 HttpMessageHandler，逐块返回响应内容
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<string> _chunks;
    private readonly HttpStatusCode _statusCode;

    public FakeHttpMessageHandler(List<string> chunks, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _chunks = chunks;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StreamContent(new ChunkedMemoryStream(_chunks))
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
        {
            CharSet = "UTF-8"
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// 模拟 chunked 流式响应的 Stream
/// 将 chunks 列表转换为可读取的流（跳过 chunk 大小行）
/// </summary>
public class ChunkedMemoryStream : Stream
{
    private readonly MemoryStream _innerStream;

    public ChunkedMemoryStream(List<string> chunks)
    {
        // HTTP chunked encoding 中，chunk 大小行只是协议分隔符
        // 原始数据是连续的字节流，chunk 之间不需要添加任何换行符
        var content = new StringBuilder();
        
        foreach (var line in chunks)
        {
            if (!IsChunkSizeLine(line))
            {
                content.Append(line);
            }
        }
        
        var bytes = Encoding.UTF8.GetBytes(content.ToString());
        _innerStream = new MemoryStream(bytes);
    }

    /// <summary>
    /// 判断是否为 chunk 大小行
    /// </summary>
    private static bool IsChunkSizeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        
        // 如果行首有空白字符（有缩进），则不是 chunk 大小行
        if (char.IsWhiteSpace(line[0])) return false;
        
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        if (trimmed.Length > 8) return false;
        
        return trimmed.All(c => 
            (c >= '0' && c <= '9') || 
            (c >= 'a' && c <= 'f') || 
            (c >= 'A' && c <= 'F'));
    }

    public override bool CanRead => true;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _innerStream.Length;
    public override long Position 
    { 
        get => _innerStream.Position; 
        set => _innerStream.Position = value; 
    }

    public override void Flush() => _innerStream.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
