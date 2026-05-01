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

public class AzureAIFoundryChatServiceTests
{
    [Fact]
    public async Task Streaming_GrokUsageShouldRespectTotalTokens()
    {
        List<string> chunks = new()
        {
            "data: {\"id\":\"41b43bde-7f82-4a80-9692-91e8b0739f52\",\"object\":\"chat.completion.chunk\",\"created\":1773405995,\"model\":\"grok-4-1-fast-reasoning\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hello\"},\"finish_reason\":null}]}\n\n",
            "data: {\"id\":\"41b43bde-7f82-4a80-9692-91e8b0739f52\",\"object\":\"chat.completion.chunk\",\"created\":1773405995,\"model\":\"grok-4-1-fast-reasoning\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n",
            "data: {\"id\":\"41b43bde-7f82-4a80-9692-91e8b0739f52\",\"object\":\"chat.completion.chunk\",\"created\":1773405995,\"model\":\"grok-4-1-fast-reasoning\",\"choices\":[],\"usage\":{\"prompt_tokens\":69,\"completion_tokens\":837,\"total_tokens\":1737,\"prompt_tokens_details\":{\"text_tokens\":69,\"audio_tokens\":0,\"image_tokens\":0,\"cached_tokens\":0},\"completion_tokens_details\":{\"reasoning_tokens\":831,\"audio_tokens\":0,\"accepted_prediction_tokens\":0,\"rejected_prediction_tokens\":0},\"num_sources_used\":0,\"cost_in_usd_ticks\":0},\"system_fingerprint\":\"fp_39c5j0a324\"}\n\n",
            "data: [DONE]\n\n"
        };
        DateTime now = DateTime.UtcNow;

        FiddlerDumpHttpClientFactory httpClientFactory = new(chunks, HttpStatusCode.OK);
        AzureAIFoundryChatService service = new(httpClientFactory);

        ModelKeySnapshot modelKeySnapshot = new()
        {
            Id = 11,
            ModelKeyId = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            Host = "https://example.services.ai.azure.com/models",
            ModelProviderId = (short)DBModelProvider.AzureAIFoundry,
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
            Name = "Grok",
            DeploymentName = "grok-4-1-fast-reasoning",
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
        Assert.Equal(69, usage.Usage.InputTokens);
        Assert.Equal(1668, usage.Usage.OutputTokens);
        Assert.Equal(831, usage.Usage.ReasoningTokens);
        Assert.Equal(1737, usage.Usage.InputTokens + usage.Usage.OutputTokens);
    }
}