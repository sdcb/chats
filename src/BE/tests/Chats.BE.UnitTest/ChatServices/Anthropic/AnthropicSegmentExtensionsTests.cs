using Chats.BE.Services.Models.Dtos;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices.Anthropic;

public class AnthropicSegmentExtensionsTests
{
    [Fact]
    public void ToAnthropicResponse_WithCachedPromptTokens_UsesFreshInputTokensInAnthropicUsage()
    {
        ChatCompletionSnapshot snapshot = new()
        {
            Segments = [],
            Usage = new ChatTokenUsage
            {
                InputTokens = 43,
                OutputTokens = 151,
                CacheTokens = 7,
                CacheCreationTokens = 9,
                ReasoningTokens = 0,
            },
            IsUsageReliable = true,
            FinishReason = DBFinishReason.Success,
        };

        var response = snapshot.ToAnthropicResponse("mimo-v2.5", "msg_1");

        Assert.Equal(36, response.Usage.InputTokens);
        Assert.Equal(151, response.Usage.OutputTokens);
        Assert.Equal(7, response.Usage.CacheReadInputTokens);
        Assert.Equal(9, response.Usage.CacheCreationInputTokens);
    }
}