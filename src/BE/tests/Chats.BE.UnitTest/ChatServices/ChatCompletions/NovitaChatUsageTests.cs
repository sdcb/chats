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

public class NovitaChatUsageTests
{
    [Fact]
    public async Task Streaming_UsageOnlyChunk_WithNullDetails_ShouldBeParsed()
    {
        List<string> chunks =
        [
            "data: {\"id\":\"39ee1a70fb6afde81f5dba5c89d20989\",\"object\":\"chat.completion.chunk\",\"created\":1774790827,\"model\":\"qwen/qwen3.5-397b-a17b\",\"choices\":[],\"system_fingerprint\":\"\",\"usage\":{\"prompt_tokens\":1771,\"completion_tokens\":58,\"total_tokens\":1829,\"prompt_tokens_details\":null,\"completion_tokens_details\":null},\"sla_metrics\":{\"ttft_ms\":810,\"ts_us\":1774790828250010}}\n\n",
            "data: [DONE]\n\n",
        ];
        DateTime now = DateTime.UtcNow;

        FiddlerDumpHttpClientFactory httpClientFactory = new(chunks, HttpStatusCode.OK);
        NovitaChatService service = new(httpClientFactory);

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (short)DBModelProvider.Novita,
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
            DeploymentName = "qwen/qwen3.5-397b-a17b",
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

        UsageChatSegment? usage = Assert.IsType<UsageChatSegment>(Assert.Single(segments));
        Assert.Equal(1771, usage.Usage.InputTokens);
        Assert.Equal(58, usage.Usage.OutputTokens);
        Assert.Equal(0, usage.Usage.ReasoningTokens);
        Assert.Equal(0, usage.Usage.CacheTokens);
    }
}
