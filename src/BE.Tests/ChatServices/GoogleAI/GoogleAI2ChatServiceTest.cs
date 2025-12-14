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
using System.Text.Json;
using Chats.BE.Tests.ChatServices.Http;

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
        return new FiddlerDumpHttpClientFactory(dump.Response.Chunks, statusCode, dump.Request.Body);
    }

    private static ChatRequest CreateBaseChatRequest(string modelDeploymentName, string prompt, Action<ChatConfig>? configure = null)
    {
        bool isFlash = modelDeploymentName.Contains("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase) &&
                       !modelDeploymentName.Contains("gemini-2.5-flash-image", StringComparison.OrdinalIgnoreCase);
        bool isFlashImage = modelDeploymentName.Contains("gemini-2.5-flash-image", StringComparison.OrdinalIgnoreCase);
        bool isImageGenerationExp = modelDeploymentName.Contains("gemini-2.0-flash-exp-image-generation", StringComparison.OrdinalIgnoreCase);
        bool isFlashExp = modelDeploymentName.Contains("gemini-2.0-flash-exp", StringComparison.OrdinalIgnoreCase);

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
            MaxResponseTokens = isImageGenerationExp ? 8192 : (isFlashExp ? 8000 : (isFlashImage ? 8192 : 0)),
            MinTemperature = 0,
            MaxTemperature = 2,
        };

        if (isFlash)
        {
            model.ReasoningEffortOptions = "1";
        }

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
            Temperature = 1.0f,
            ReasoningEffortId = isFlash ? (byte)DBReasoningEffort.Minimal : (byte)DBReasoningEffort.Default,
            SystemPrompt = null,
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
            cfg.SystemPrompt = GoogleAiDumpExtractors.TryGetSystemPrompt(dump.Request.Body);
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
            cfg.SystemPrompt = GoogleAiDumpExtractors.TryGetSystemPrompt(dump.Request.Body);
        });
        request = request with
        {
            Tools =
            [
                new FunctionTool
                {
                    FunctionName = "run_code",
                    FunctionDescription = GoogleAiDumpExtractors.TryGetFirstFunctionDescription(dump.Request.Body) ?? "执行C#代码",
                    FunctionParameters = """{"type":"object","properties":{"code":{"type":"string"},"timeout":{"type":"integer"}},"required":["code"]}"""
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
            cfg.SystemPrompt = GoogleAiDumpExtractors.TryGetSystemPrompt(dump.Request.Body);
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

        // 使用与录制 Fiddler dump 一致的图片生成模型名称
        var request = CreateBaseChatRequest("gemini-2.5-flash-image", "生成一张小猫的图片");

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

        var request = CreateBaseChatRequest("gemini-2.0-flash-exp-image-generation", "生成一张小猫的图片") with
        {
            Tools =
            [
                new FunctionTool
                {
                    FunctionName = "run_code",
                    FunctionDescription = GoogleAiDumpExtractors.TryGetFirstFunctionDescription(dump.Request.Body) ?? "执行C#代码",
                    FunctionParameters = """{"type":"object","properties":{"code":{"type":"string"},"timeout":{"type":"integer"}},"required":["code"]}"""
                }
            ]
        };

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

        var request = CreateBaseChatRequest("gemini-2.0-flash-exp", "生成一张小猫的图片") with
        {
            Tools =
            [
                new FunctionTool
                {
                    FunctionName = "run_code",
                    FunctionDescription = GoogleAiDumpExtractors.TryGetFirstFunctionDescription(dump.Request.Body) ?? "执行C#代码",
                    FunctionParameters = """{"type":"object","properties":{"code":{"type":"string"},"timeout":{"type":"integer"}},"required":["code"]}"""
                }
            ]
        };

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

internal static class GoogleAiDumpExtractors
{
    public static string? TryGetSystemPrompt(string? requestBodyJson)
    {
        if (string.IsNullOrWhiteSpace(requestBodyJson))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(requestBodyJson);
            if (!doc.RootElement.TryGetProperty("systemInstruction", out JsonElement sys))
            {
                return null;
            }

            if (!sys.TryGetProperty("parts", out JsonElement parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = parts[0];
            if (!first.TryGetProperty("text", out JsonElement text) || text.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return text.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? TryGetFirstFunctionDescription(string? requestBodyJson)
    {
        if (string.IsNullOrWhiteSpace(requestBodyJson))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(requestBodyJson);
            if (!doc.RootElement.TryGetProperty("tools", out JsonElement tools) || tools.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement tool in tools.EnumerateArray())
            {
                if (!tool.TryGetProperty("functionDeclarations", out JsonElement decls) || decls.ValueKind != JsonValueKind.Array || decls.GetArrayLength() == 0)
                {
                    continue;
                }

                JsonElement decl = decls[0];
                if (!decl.TryGetProperty("description", out JsonElement desc) || desc.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                return desc.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
