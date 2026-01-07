using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Net;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class MimoChatServiceTest
{
    private const string TestDataPath = "ChatServices/ChatCompletions/FiddlerDump";

    private static IHttpClientFactory CreateMockHttpClientFactory(FiddlerHttpDumpParser.HttpDump dump, bool validateRequest = true)
    {
        var statusCode = (HttpStatusCode)dump.Response.StatusCode;
        // SSE requires newlines between events, but FiddlerHttpDumpParser strips them.
        // We add them back here for the mock stream.
        var chunksWithNewlines = dump.Response.Chunks.Select(c => c + "\n").ToList();
        return new FiddlerDumpHttpClientFactory(chunksWithNewlines, statusCode, validateRequest ? dump.Request.Body : null);
    }

    [Fact]
    public async Task NonStreaming_WithNullToolCalls_ShouldNotThrow()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "XiaomiMimo-NonStream.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var service = new MimoChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.OpenAI,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "mimo-v2-flash",
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowStreaming = true,
        };

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        var request = new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = false, // This triggers ChatNonStreaming
            EndUserId = "8"
        };

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);
        var textSegment = segments.OfType<TextChatSegment>().FirstOrDefault();
        Assert.NotNull(textSegment);
        Assert.Equal("Hello! How can I help you today?", textSegment.Text);
    }

    [Fact]
    public async Task Streaming_WithInterleavedToolCallBug_ShouldParseToolCall()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "XiaomiMimo-InterleavedToolCallBug.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump, validateRequest: false);

        var service = new MimoChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.OpenAI,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "mimo-v2-flash",
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowStreaming = true,
        };

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        var request = new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8"
        };

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        // Currently, this will fail because it will only find ThinkChatSegments and no ToolCallSegments
        var toolCalls = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCalls);
        
        var firstToolCall = toolCalls.First(tc => tc.Name != null);
        Assert.Equal("run_csharp", firstToolCall.Name);
        
        var allArguments = string.Join("", toolCalls.Where(tc => tc.Index == firstToolCall.Index).Select(tc => tc.Arguments));
        Assert.Contains("using System;", allArguments);
        Assert.Contains("\"timeout\": 30000", allArguments);

        Assert.All(toolCalls, tc => Assert.Matches(@"^call_[a-f0-9]{24}$", tc.Id));

        var finishReason = segments.OfType<FinishReasonChatSegment>().LastOrDefault();
        Assert.NotNull(finishReason);
        Assert.Equal(DBFinishReason.ToolCalls, finishReason.FinishReason);
    }

    [Fact]
    public async Task Streaming_NormalToolCall_ShouldParseCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "XiaomiMimo-ToolCall.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump, validateRequest: false);

        var service = new MimoChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.OpenAI,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "mimo-v2-flash",
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowStreaming = true,
        };

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        var request = new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8"
        };

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        var toolCalls = segments.OfType<ToolCallSegment>().ToList();
        Assert.NotEmpty(toolCalls);
        
        var toolCall = toolCalls.First(tc => tc.Id != null);
        Assert.Equal("call_13f2f94b48d240a8ae062fe0", toolCall.Id);
        Assert.Equal("run_csharp", toolCall.Name);
        
        var allArguments = string.Join("", toolCalls.Where(tc => tc.Index == toolCall.Index).Select(tc => tc.Arguments));
        Assert.Contains("1234.0 / 5432.0", allArguments);

        var finishReason = segments.OfType<FinishReasonChatSegment>().LastOrDefault();
        Assert.NotNull(finishReason);
        Assert.Equal(DBFinishReason.ToolCalls, finishReason.FinishReason);
    }
}
