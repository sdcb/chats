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
        var statusCode = (HttpStatusCode)dump.Response.StatusCode;
        // SSE requires newlines between events, but FiddlerHttpDumpParser strips them.
        // We add them back here for the mock stream.
        var chunksWithNewlines = dump.Response.Chunks.Select(c => c + "\n").ToList();
        return new FiddlerDumpHttpClientFactory(chunksWithNewlines, statusCode, validateRequest ? dump.Request.Body : null);
    }

    [Fact]
    public async Task Streaming_MoonshotUsageTopLevelCachedTokens_ShouldBeParsed()
    {
        var filePath = Path.Combine(TestDataPath, "Moonshot.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump, validateRequest: false);

        var service = new MoonshotChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.Moonshot,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "kimi-k2-thinking-turbo",
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowStreaming = true,
            ApiTypeId = (byte)DBApiType.OpenAIChatCompletion,
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

        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        UsageChatSegment? usage = segments.OfType<UsageChatSegment>().LastOrDefault();
        Assert.NotNull(usage);
        Assert.Equal(2304, usage.Usage.CacheTokens);
    }
}
