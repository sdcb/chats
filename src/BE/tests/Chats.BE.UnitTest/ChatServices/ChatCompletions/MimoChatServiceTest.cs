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
        HttpStatusCode statusCode = (HttpStatusCode)dump.Response.StatusCode;
        // SSE requires newlines between events, but FiddlerHttpDumpParser strips them.
        // We add them back here for the mock stream.
        List<string> chunksWithNewlines = dump.Response.Chunks.Select(c => c + "\n").ToList();
        return new FiddlerDumpHttpClientFactory(chunksWithNewlines, statusCode, validateRequest ? dump.Request.Body : null);
    }

    private static ChatConfig CreateChatConfig()
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (short)DBModelProvider.OpenAI,
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
            DeploymentName = "mimo-v2-flash",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowStreaming = true,
            ApiTypeId = (byte)DBApiType.OpenAIChatCompletion,
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

        return new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };
    }

    [Fact]
    public async Task Streaming_NormalToolCall_ShouldParseCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "XiaomiMimo-ToolCall.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump, validateRequest: false);

        MimoChatService service = new(httpClientFactory);

        var chatConfig = CreateChatConfig();

        ChatRequest request = new()
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8"
        };

        // Act
        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        List<ToolCallSegment> toolCalls = segments.OfType<ToolCallSegment>().ToList();
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
