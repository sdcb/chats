using System.Net;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.UnitTest.ChatServices.Http;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.Anthropic;

public class DeepSeekAnthropicServiceTests
{
    private static IHttpClientFactory CreateMockHttpClientFactory(params string[] chunks)
    {
        return new FiddlerDumpHttpClientFactory([.. chunks], HttpStatusCode.OK);
    }

    private static ChatRequest CreateRequest()
    {
        DateTime now = DateTime.UtcNow;

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://api.deepseek.com/anthropic",
            ModelProviderId = (short)DBModelProvider.DeepSeek,
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
            DeploymentName = "deepseek-reasoner",
            ModelKeyId = modelKey.Id,
            ModelKeySnapshotId = modelKeySnapshot.Id,
            ModelKeySnapshot = modelKeySnapshot,
            AllowStreaming = true,
            MaxResponseTokens = 2048,
            ApiTypeId = (byte)DBApiType.AnthropicMessages,
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
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = true,
            EndUserId = "8",
        };
    }

    [Fact]
    public async Task ChatStreamed_MessageDeltaWithoutInputTokens_PreservesPreviousInputTokens()
    {
        IHttpClientFactory httpClientFactory = CreateMockHttpClientFactory(
            "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"deepseek-reasoner\",\"content\":[],\"stop_reason\":null,\"stop_sequence\":null,\"usage\":{\"input_tokens\":36,\"output_tokens\":0}}}\n\n",
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":151}}\n\n",
            "data: {\"type\":\"message_stop\"}\n\n"
        );
        DeepSeekAnthropicService service = new(httpClientFactory);
        ChatRequest request = CreateRequest();

        List<ChatSegment> segments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        List<UsageChatSegment> usageSegments = segments.OfType<UsageChatSegment>().ToList();
        Assert.Equal(2, usageSegments.Count);
        Assert.Equal(36, usageSegments[0].Usage.InputTokens);
        Assert.Equal(0, usageSegments[0].Usage.OutputTokens);
        Assert.Equal(36, usageSegments[1].Usage.InputTokens);
        Assert.Equal(151, usageSegments[1].Usage.OutputTokens);

        FinishReasonChatSegment? finishReason = segments.OfType<FinishReasonChatSegment>().LastOrDefault();
        Assert.NotNull(finishReason);
        Assert.Equal(DBFinishReason.Success, finishReason.FinishReason);
    }

    [Fact]
    public async Task ChatStreamed_MessageDeltaWithoutCacheTokens_PreservesPreviousCacheTokens()
    {
        IHttpClientFactory httpClientFactory = CreateMockHttpClientFactory(
            "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"deepseek-reasoner\",\"content\":[],\"stop_reason\":null,\"stop_sequence\":null,\"usage\":{\"input_tokens\":36,\"cache_creation_input_tokens\":9,\"cache_read_input_tokens\":7,\"output_tokens\":0}}}\n\n",
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":151}}\n\n",
            "data: {\"type\":\"message_stop\"}\n\n"
        );
        DeepSeekAnthropicService service = new(httpClientFactory);
        ChatRequest request = CreateRequest();

        List<UsageChatSegment> usageSegments = [];
        await foreach (ChatSegment segment in service.ChatStreamed(request, CancellationToken.None))
        {
            if (segment is UsageChatSegment usage)
            {
                usageSegments.Add(usage);
            }
        }

        Assert.Equal(2, usageSegments.Count);
        UsageChatSegment finalUsage = usageSegments[1];
        Assert.Equal(36, finalUsage.Usage.InputTokens);
        Assert.Equal(151, finalUsage.Usage.OutputTokens);
        Assert.Equal(7, finalUsage.Usage.CacheTokens);
        Assert.Equal(9, finalUsage.Usage.CacheCreationTokens);
    }
}