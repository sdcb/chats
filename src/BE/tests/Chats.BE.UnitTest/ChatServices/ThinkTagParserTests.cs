using System.Net;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices;

public class ThinkTagParserTests
{
    private const string TestDataPath = "ChatServices/ChatCompletions/FiddlerDump";

    private static IHttpClientFactory CreateMockHttpClientFactory(FiddlerHttpDumpParser.HttpDump dump, bool validateRequest = true)
    {
        var statusCode = (HttpStatusCode)dump.Response.StatusCode;
        var chunksWithNewlines = dump.Response.Chunks.Select(c => c + "\n").ToList();
        return new FiddlerDumpHttpClientFactory(chunksWithNewlines, statusCode, validateRequest ? dump.Request.Body : null);
    }

    [Fact]
    public async Task TokenPonyMinimaxM25Dump_ShouldParseThinkTagIntoReasoningSegment()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "TokenPony-MinimaxM2.5.dump");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump, validateRequest: false);
        var service = new TokenPonyChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.TokenPony,
        };

        var model = new Model
        {
            Id = 1,
            Name = "TokenPony Minimax",
            DeploymentName = "minimax-m2.5",
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowStreaming = true,
            ThinkTagParserEnabled = true,
        };

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        var request = new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("计算12345/54321=?")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8"
        };

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatEntry(request, null!, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.Contains(segments, s => s is ThinkChatSegment);
    }

    [Fact]
    public async Task ThinkTagParser_WhenThinkTagIsNotAtStart_ShouldTreatAllAsResponse()
    {
        // Arrange
        UsageChatSegment usage = new()
        {
            Usage = new ChatTokenUsage
            {
                InputTokens = 1,
                OutputTokens = 2,
            }
        };

        var tokens = ToAsyncEnumerable(
        [
            ChatSegment.FromText("blabla<think>"),
            usage,
            ChatSegment.FromText("secret"),
            ChatSegment.FromText("</think>"),
            ChatSegment.FromText("done")
        ]);

        // Act
        var parsed = new List<ChatSegment>();
        await foreach (var segment in ThinkTagParser.Parse(tokens))
        {
            parsed.Add(segment);
        }

        // Assert
        Assert.DoesNotContain(parsed, s => s is ThinkChatSegment);
        Assert.Contains(parsed.OfType<TextChatSegment>(), s => s.Text.Contains("blabla<think>", StringComparison.Ordinal));
        Assert.Contains(parsed, s => ReferenceEquals(s, usage));
    }

    private static async IAsyncEnumerable<ChatSegment> ToAsyncEnumerable(IEnumerable<ChatSegment> values)
    {
        foreach (var value in values)
        {
            yield return value;
            await Task.Yield();
        }
    }
}
