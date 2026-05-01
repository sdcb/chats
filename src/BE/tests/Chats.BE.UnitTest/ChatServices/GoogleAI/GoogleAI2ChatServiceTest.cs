using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.GoogleAI;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.GoogleAI;

public class GoogleAI2ChatServiceTest
{
    private const string TestDataPath = "ChatServices/GoogleAI/FiddlerDump";

    /// <summary>
    /// 基于 Fiddler dump 文件创建模拟的 HttpClientFactory
    /// </summary>
    private static IHttpClientFactory CreateMockHttpClientFactory(FiddlerHttpDumpParser.HttpDump dump)
    {
        HttpStatusCode statusCode = (HttpStatusCode)dump.Response.StatusCode;
        return new FiddlerDumpHttpClientFactory(dump.Response.Chunks, statusCode, dump.Request.Body);
    }

    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static ChatRequest CreateBaseChatRequest(string modelDeploymentName, string prompt, Action<ChatConfig>? configure = null)
    {
        bool isFlash = (modelDeploymentName.Contains("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase) ||
                        modelDeploymentName.Contains("gemini-3-flash", StringComparison.OrdinalIgnoreCase)) &&
                       !modelDeploymentName.Contains("gemini-2.5-flash-image", StringComparison.OrdinalIgnoreCase);
        bool isFlashImage = modelDeploymentName.Contains("gemini-2.5-flash-image", StringComparison.OrdinalIgnoreCase);
        bool isImageGenerationExp = modelDeploymentName.Contains("gemini-2.0-flash-exp-image-generation", StringComparison.OrdinalIgnoreCase);
        bool isFlashExp = modelDeploymentName.Contains("gemini-2.0-flash-exp", StringComparison.OrdinalIgnoreCase);
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = null,
            ModelProviderId = 1,
            CreatedAt = now,
        };

        ModelKey modelKey = new()
        {
            Id = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CurrentSnapshotId = modelKeySnapshot.Id,
            CurrentSnapshot = modelKeySnapshot,
        };

        modelKeySnapshot.ModelKey = modelKey;

        ModelSnapshot modelSnapshot = new()
        {
            Id = 21,
            ModelId = 1,
            Name = "Test Model",
            DeploymentName = modelDeploymentName,
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowSearch = true,
            AllowVision = true,
            AllowStreaming = true,
            AllowCodeExecution = true,
            AllowToolCall = true,
            ContextWindow = 128000,
            MaxResponseTokens = isImageGenerationExp ? 8192 : (isFlashExp ? 8000 : (isFlashImage ? 8192 : 0)),
            MinTemperature = 0,
            MaxTemperature = 2,
            ReasoningEffortOptions = isFlash ? "1" : null,
            CreatedAt = now,
        };

        Model model = new()
        {
            Id = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CurrentSnapshotId = modelSnapshot.Id,
            CurrentSnapshot = modelSnapshot,
        };

        modelSnapshot.Model = model;

        ChatConfig chatConfig = new()
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

    private static JsonObject BuildNativeRequestBody(GoogleAI2ChatService service, ChatRequest request, bool allowImageGeneration)
    {
        MethodInfo method = typeof(GoogleAI2ChatService).GetMethod("BuildNativeRequestBody", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildNativeRequestBody method not found.");

        return (JsonObject?)method.Invoke(service, [request, allowImageGeneration])
            ?? throw new InvalidOperationException("BuildNativeRequestBody returned null.");
    }

    [Fact]
    public void BuildNativeRequestBody_ToolMessageWithBlobAttachment_EmbedsFunctionResponseParts()
    {
        GoogleAI2ChatService service = new(new DummyHttpClientFactory());
        ChatRequest request = CreateBaseChatRequest("gemini-3-flash-preview", "show chart") with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralToolCallContent.Create("call_1", "draw_chart", "{}")
                ),
                NeutralMessage.FromTool(
                    NeutralToolCallResponseContent.Create("call_1", "{\"status\":\"ok\"}"),
                    NeutralFileBlobContent.Create([1, 2, 3], "image/png")
                )
            ]
        };

        JsonObject body = BuildNativeRequestBody(service, request, allowImageGeneration: false);
        JsonArray contents = Assert.IsType<JsonArray>(body["contents"]);

        JsonObject functionMessage = contents
            .Select(node => Assert.IsType<JsonObject>(node))
            .First(content => (string?)content["role"] == "function");

        JsonArray parts = Assert.IsType<JsonArray>(functionMessage["parts"]);
        JsonObject functionResponsePart = Assert.IsType<JsonObject>(parts[0]);
        JsonObject functionResponse = Assert.IsType<JsonObject>(functionResponsePart["functionResponse"]);
        Assert.Equal("call_1", (string?)functionResponse["name"]);

        JsonArray multimodalParts = Assert.IsType<JsonArray>(functionResponse["parts"]);
        JsonObject inlineData = Assert.IsType<JsonObject>(multimodalParts[0]?["inlineData"]);
        Assert.Equal("image/png", (string?)inlineData["mimeType"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), (string?)inlineData["data"]);
    }

    [Fact]
    public void BuildNativeRequestBody_AssistantToolCallWithEmptyParameters_UsesEmptyArgsObject()
    {
        GoogleAI2ChatService service = new(new DummyHttpClientFactory());
        ChatRequest request = CreateBaseChatRequest("gemini-3-flash-preview", "create session") with
        {
            Messages =
            [
                NeutralMessage.FromAssistant(
                    NeutralToolCallContent.Create("call_1", "create_docker_session", "")
                )
            ]
        };

        JsonObject body = BuildNativeRequestBody(service, request, allowImageGeneration: false);
        JsonArray contents = Assert.IsType<JsonArray>(body["contents"]);

        JsonObject modelMessage = contents
            .Select(node => Assert.IsType<JsonObject>(node))
            .First(content => (string?)content["role"] == "model");

        JsonArray parts = Assert.IsType<JsonArray>(modelMessage["parts"]);
        JsonObject functionCall = Assert.IsType<JsonObject>(parts[0]?["functionCall"]);
        JsonObject args = Assert.IsType<JsonObject>(functionCall["args"]);
        Assert.Empty(args);
    }

    [Fact]
    public void BuildNativeRequestBody_FunctionToolWithNullableSchema_UsesGoogleNullableFields()
    {
        GoogleAI2ChatService service = new(new DummyHttpClientFactory());
        ChatRequest request = CreateBaseChatRequest("gemini-3-flash-preview", "create session") with
        {
            Tools =
                [
                        new FunctionTool
                                {
                                        FunctionName = "create_docker_session",
                                        FunctionDescription = "Create a docker session.",
                                        FunctionParameters =
                                        """
                                        {
                                            "type": "object",
                                            "properties": {
                                                "image": {
                                                    "type": [
                                                        "string",
                                                        "null"
                                                    ],
                                                    "description": "Docker image to use."
                                                },
                                                "networkMode": {
                                                    "type": [
                                                        "string",
                                                        "null"
                                                    ],
                                                    "description": "Network mode.",
                                                    "enum": [
                                                        "none",
                                                        "bridge",
                                                        "host"
                                                    ]
                                                }
                                            }
                                        }
                                        """
                                }
                ]
        };

        JsonObject body = BuildNativeRequestBody(service, request, allowImageGeneration: false);
        JsonArray tools = Assert.IsType<JsonArray>(body["tools"]);
        JsonObject functionTool = Assert.IsType<JsonObject>(tools[0]);
        JsonArray declarations = Assert.IsType<JsonArray>(functionTool["functionDeclarations"]);
        JsonObject declaration = Assert.IsType<JsonObject>(declarations[0]);
        JsonObject parameters = Assert.IsType<JsonObject>(declaration["parameters"]);
        JsonObject properties = Assert.IsType<JsonObject>(parameters["properties"]);

        JsonObject image = Assert.IsType<JsonObject>(properties["image"]);
        Assert.Equal("STRING", image["type"]!.GetValue<string>());
        Assert.True(image["nullable"]!.GetValue<bool>());
        Assert.Equal("Docker image to use.", image["description"]!.GetValue<string>());

        JsonObject networkMode = Assert.IsType<JsonObject>(properties["networkMode"]);
        Assert.Equal("STRING", networkMode["type"]!.GetValue<string>());
        Assert.True(networkMode["nullable"]!.GetValue<bool>());
        JsonArray enumValues = Assert.IsType<JsonArray>(networkMode["enum"]);
        Assert.Equal(["none", "bridge", "host"], enumValues.Select(node => node!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void GetUsage_WithCachedContentTokenCount_UsesCachedTokens()
    {
        JsonObject usageMetadata = new()
        {
            ["promptTokenCount"] = 14347,
            ["candidatesTokenCount"] = 215,
            ["totalTokenCount"] = 14598,
            ["cachedContentTokenCount"] = 8010,
            ["thoughtsTokenCount"] = 36
        };

        using JsonDocument doc = JsonDocument.Parse(usageMetadata.ToJsonString());
        ChatTokenUsage usage = GoogleAI2ChatService.GetUsage(doc.RootElement)
            ?? throw new InvalidOperationException("GetUsage returned null.");

        Assert.Equal(14347, usage.InputTokens);
        Assert.Equal(8010, usage.CacheTokens);
        Assert.Equal(36, usage.ReasoningTokens);
        Assert.Equal(6337, usage.InputFreshTokens);
    }

    [Fact]
    public async Task CodeExecute_ShouldReturnCodeExecutionResult()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "CodeExecute.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        ChatCompletionService chatCompletionService = new(httpClientFactory);
        GoogleAI2ChatService service = new(httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.5-flash", "调用内置工具，计算1234/5432=?", cfg =>
        {
            cfg.SystemPrompt = GoogleAiDumpExtractors.TryGetSystemPrompt(dump.Request.Body);
        });
        request.ModelProviderCodeExecutionEnabled = true;

        // Act
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);

        // 应该有思考内容（thought）
        List<ThinkChatSegment> thinkSegments = segments.OfType<ThinkChatSegment>().ToList();
        Assert.NotEmpty(thinkSegments);

        // 应该有代码执行工具调用
        List<ToolCallSegment> toolCallSegments = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCallSegments);
        var codeExecCall = toolCallSegments.First();
        Assert.Equal("PYTHON", codeExecCall.Name);
        Assert.Contains("1234", codeExecCall.Arguments);
        Assert.Contains("5432", codeExecCall.Arguments);

        // 应该有代码执行结果
        List<ToolCallResponseSegment> toolResponseSegments = segments.OfType<ToolCallResponseSegment>().ToList();
        Assert.NotEmpty(toolResponseSegments);
        Assert.True(toolResponseSegments.First().IsSuccess);
        Assert.Contains("0.227", toolResponseSegments.First().Response);

        // 应该有文本输出
        List<TextChatSegment> textSegments = segments.OfType<TextChatSegment>().ToList();
        Assert.NotEmpty(textSegments);

        // 应该有usage信息
        List<UsageChatSegment> usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
        Assert.True(usageSegments.Last().Usage.InputTokens > 0);

        // 应该有finish reason
        List<FinishReasonChatSegment> finishSegments = segments.OfType<FinishReasonChatSegment>().ToList();
        Assert.NotEmpty(finishSegments);
        Assert.Equal(DBFinishReason.Success, finishSegments.Last().FinishReason);
    }

    [Fact]
    public async Task ToolCall_ShouldReturnFunctionCall()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ToolCall.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        ChatCompletionService chatCompletionService = new(httpClientFactory);
        GoogleAI2ChatService service = new(httpClientFactory);

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
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);

        // 应该有函数调用
        List<ToolCallSegment> toolCallSegments = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCallSegments);
        var functionCall = toolCallSegments.First();
        Assert.Equal("run_code", functionCall.Name);
        Assert.Contains("1234", functionCall.Arguments);
        Assert.Contains("5432", functionCall.Arguments);

        // 应该有usage信息
        List<UsageChatSegment> usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
    }

    [Fact]
    public async Task WebSearch_ShouldReturnSearchResults()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "WebSearch.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        ChatCompletionService chatCompletionService = new(httpClientFactory);
        GoogleAI2ChatService service = new(httpClientFactory);

        var request = CreateBaseChatRequest("gemini-2.5-flash", "今天有什么新闻？", cfg =>
        {
            cfg.WebSearchEnabled = true;
            cfg.SystemPrompt = GoogleAiDumpExtractors.TryGetSystemPrompt(dump.Request.Body);
        });

        // Act
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);

        // 应该有思考内容
        List<ThinkChatSegment> thinkSegments = segments.OfType<ThinkChatSegment>().ToList();
        Assert.NotEmpty(thinkSegments);

        // 应该有文本输出
        List<TextChatSegment> textSegments = segments.OfType<TextChatSegment>().ToList();
        Assert.NotEmpty(textSegments);

        // 应该有usage信息
        List<UsageChatSegment> usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);

        // 应该有finish reason
        List<FinishReasonChatSegment> finishSegments = segments.OfType<FinishReasonChatSegment>().ToList();
        Assert.NotEmpty(finishSegments);
        Assert.Equal(DBFinishReason.Success, finishSegments.Last().FinishReason);
    }

    [Fact]
    public async Task ImageGenerate_ShouldReturnImage()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ImageGenerate.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        ChatCompletionService chatCompletionService = new(httpClientFactory);
        GoogleAI2ChatService service = new(httpClientFactory);

        // 使用与录制 Fiddler dump 一致的图片生成模型名称
        var request = CreateBaseChatRequest("gemini-2.5-flash-image", "生成一张小猫的图片");

        // Act
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);

        // 应该有文本输出
        List<TextChatSegment> textSegments = segments.OfType<TextChatSegment>().ToList();
        Assert.NotEmpty(textSegments);

        // 应该有图片输出
        List<ImageChatSegment> imageSegments = segments.OfType<ImageChatSegment>().ToList();
        Assert.NotEmpty(imageSegments);
        Base64Image? image = imageSegments.First() as Base64Image;
        Assert.NotNull(image);
        Assert.Equal("image/png", image.ContentType);
        Assert.NotEmpty(image.Base64);

        // 应该有usage信息
        List<UsageChatSegment> usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.NotEmpty(usageSegments);
    }

    [Fact]
    public async Task Error404_ShouldThrowRawChatServiceException()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "Error_404.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        ChatCompletionService chatCompletionService = new(httpClientFactory);
        GoogleAI2ChatService service = new(httpClientFactory);

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
        var filePath = Path.Combine(TestDataPath, "Error_429.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        ChatCompletionService chatCompletionService = new(httpClientFactory);
        GoogleAI2ChatService service = new(httpClientFactory);

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

    [Fact]
    public async Task ThoughtSignature_ShouldBeDiscardedIfAfterText()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "ThoughtSignature.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);

        GoogleAI2ChatService service = new(httpClientFactory);

        var request = CreateBaseChatRequest("gemini-3-flash-preview", "你好，你是谁？", cfg =>
        {
            cfg.SystemPrompt = GoogleAiDumpExtractors.TryGetSystemPrompt(dump.Request.Body);
            cfg.MaxOutputTokens = 65536;
            cfg.ReasoningEffortId = (byte)DBReasoningEffort.Minimal;
        });

        // Act
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        List<ThinkChatSegment> thinkSegments = segments.OfType<ThinkChatSegment>().ToList();
        List<TextChatSegment> textSegments = segments.OfType<TextChatSegment>().ToList();

        Assert.NotEmpty(thinkSegments);
        Assert.NotEmpty(textSegments);

        // 确保最后一个 text 之后没有 think
        int lastTextIndex = segments.FindLastIndex(s => s is TextChatSegment);
        int lastThinkIndex = segments.FindLastIndex(s => s is ThinkChatSegment);
        Assert.True(lastThinkIndex < lastTextIndex, "Think segment should not appear after text segment");
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
