using System.Net;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.ChatCompletions;

public class ChatCompletionReasoningTests
{
    private static ChatRequest CreateRequest(bool streamed)
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://api.openai.com/v1",
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
            Name = "Coding",
            DeploymentName = "Coding",
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

        return new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("45 / 54321")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = streamed,
        };
    }

    [Fact]
    public async Task Streaming_WhenDeltaHasReasoningFallbackAndContent_ShouldYieldReasoningBeforeContent()
    {
        List<string> chunks =
        [
            "data: {\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\",\"created\":1785739215,\"model\":\"Coding\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"123\",\"reasoning_content\":null,\"reasoning\":\"5 decimal places).\"},\"finish_reason\":null}]}\n\n",
            "data: {\"id\":\"chatcmpl-test\",\"object\":\"chat.completion.chunk\",\"created\":1785739215,\"model\":\"Coding\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n",
            "data: [DONE]\n\n",
        ];

        ReplayHttpClientFactory httpClientFactory = new(string.Concat(chunks), HttpStatusCode.OK);
        ChatCompletionService service = new(httpClientFactory);
        ChatRequest request = CreateRequest(streamed: true);

        List<ChatSegment> segments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        ThinkChatSegment thinkSegment = Assert.IsType<ThinkChatSegment>(segments[0]);
        TextChatSegment textSegment = Assert.IsType<TextChatSegment>(segments[1]);
        Assert.Equal("5 decimal places).", thinkSegment.Think);
        Assert.Equal("123", textSegment.Text);
    }

    [Fact]
    public async Task NonStreaming_WhenMessageHasReasoningFallbackAndContent_ShouldYieldReasoningBeforeContent()
    {
        List<string> chunks =
        [
            "{\"id\":\"chatcmpl-test\",\"object\":\"chat.completion\",\"created\":1785739215,\"model\":\"Coding\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"123\",\"reasoning_content\":null,\"reasoning\":\"5 decimal places).\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2,\"total_tokens\":3}}",
        ];

        ReplayHttpClientFactory httpClientFactory = new(string.Concat(chunks), HttpStatusCode.OK);
        ChatCompletionService service = new(httpClientFactory);
        ChatRequest request = CreateRequest(streamed: false);

        List<ChatSegment> segments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        ThinkChatSegment thinkSegment = Assert.IsType<ThinkChatSegment>(segments[0]);
        TextChatSegment textSegment = Assert.IsType<TextChatSegment>(segments[1]);
        Assert.Equal("5 decimal places).", thinkSegment.Think);
        Assert.Equal("123", textSegment.Text);
    }
}
