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

        var httpClientFactory = new FiddlerDumpHttpClientFactory(chunks, HttpStatusCode.OK);
        var service = new NovitaChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.Novita,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "qwen/qwen3.5-397b-a17b",
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

        UsageChatSegment? usage = Assert.IsType<UsageChatSegment>(Assert.Single(segments));
        Assert.Equal(1771, usage.Usage.InputTokens);
        Assert.Equal(58, usage.Usage.OutputTokens);
        Assert.Equal(0, usage.Usage.ReasoningTokens);
        Assert.Equal(0, usage.Usage.CacheTokens);
    }
}
