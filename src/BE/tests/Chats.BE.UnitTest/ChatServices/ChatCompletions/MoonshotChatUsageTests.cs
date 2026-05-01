using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;
using System.Net;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class MoonshotChatUsageTests
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

    [Fact]
    public async Task Streaming_MoonshotUsageTopLevelCachedTokens_ShouldBeParsed()
    {
        var filePath = Path.Combine(TestDataPath, "Moonshot.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump, validateRequest: false);
        DateTime now = DateTime.UtcNow;

        MoonshotChatService service = new(httpClientFactory);

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (short)DBModelProvider.Moonshot,
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
            DeploymentName = "kimi-k2-thinking-turbo",
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

        ChatConfig chatConfig = new()
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        ChatRequest request = new()
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8"
        };

        List<ChatSegment> segments = new();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        UsageChatSegment? usage = segments.OfType<UsageChatSegment>().LastOrDefault();
        Assert.NotNull(usage);
        Assert.Equal(2304, usage.Usage.CacheTokens);
    }
}
