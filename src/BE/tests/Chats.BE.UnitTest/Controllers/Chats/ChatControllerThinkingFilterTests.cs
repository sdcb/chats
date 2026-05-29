using Chats.BE.Controllers.Chats.Chats;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.Controllers.Chats;

public class ChatControllerThinkingFilterTests
{
    [Fact]
    public void RemoveNonMatchingHistoricalTurnThinkingBlocks_KeepsOnlyTrailingSameModelAssistantTurns()
    {
        const short deepSeek = 1;
        const short gpt = 2;
        ChatTurn[] turns =
        [
            AssistantTurn(1, deepSeek, AssistantStep(1, "deepseek-1")),
            UserTurn(2),
            AssistantTurn(3, deepSeek, AssistantStep(2, "deepseek-2")),
            UserTurn(4),
            AssistantTurn(5, gpt, AssistantStep(3, "gpt-1")),
            UserTurn(6),
            AssistantTurn(7, deepSeek, AssistantStep(4, "deepseek-3")),
            UserTurn(8),
            AssistantTurn(9, gpt, AssistantStep(5, "gpt-2")),
            UserTurn(10),
            AssistantTurn(11, gpt, AssistantStep(6, "gpt-3")),
        ];

        IReadOnlyList<Step> result = ChatController.RemoveNonMatchingHistoricalTurnThinkingBlocks(turns, gpt);

        AssertThinkRemoved(result, "deepseek-1");
        AssertThinkRemoved(result, "deepseek-2");
        AssertThinkRemoved(result, "gpt-1");
        AssertThinkRemoved(result, "deepseek-3");
        AssertThinkKept(result, "gpt-2");
        AssertThinkKept(result, "gpt-3");
    }

    [Fact]
    public void RemoveNonMatchingHistoricalTurnThinkingBlocks_DropsAllWhenLastAssistantModelDiffers()
    {
        const short deepSeek = 1;
        const short gpt = 2;
        ChatTurn[] turns =
        [
            AssistantTurn(1, gpt, AssistantStep(1, "gpt-1")),
            UserTurn(2),
            AssistantTurn(3, gpt, AssistantStep(2, "gpt-2")),
            UserTurn(4),
            AssistantTurn(5, deepSeek, AssistantStep(3, "deepseek-1")),
        ];

        IReadOnlyList<Step> result = ChatController.RemoveNonMatchingHistoricalTurnThinkingBlocks(turns, gpt);

        AssertThinkRemoved(result, "gpt-1");
        AssertThinkRemoved(result, "gpt-2");
        AssertThinkRemoved(result, "deepseek-1");
    }

    [Fact]
    public void RemoveNonMatchingHistoricalTurnThinkingBlocks_UserTurnsDoNotBreakSameModelSuffix()
    {
        const short gpt = 2;
        ChatTurn[] turns =
        [
            AssistantTurn(1, gpt, AssistantStep(1, "gpt-1")),
            UserTurn(2),
            AssistantTurn(3, gpt, AssistantStep(2, "gpt-2")),
        ];

        IReadOnlyList<Step> result = ChatController.RemoveNonMatchingHistoricalTurnThinkingBlocks(turns, gpt);

        AssertThinkKept(result, "gpt-1");
        AssertThinkKept(result, "gpt-2");
    }

    [Fact]
    public void RemoveNonMatchingHistoricalTurnThinkingBlocks_KeepsAllStepsInsidePreservedTurn()
    {
        const short gpt = 2;
        ChatTurn[] turns =
        [
            AssistantTurn(
                1,
                gpt,
                AssistantStep(1, "call-1-thinking", StepContent.FromTool("call_1", "tool", "{}")),
                ToolStep(2, "call_1"),
                AssistantStep(3, "call-2-thinking", StepContent.FromTool("call_2", "tool", "{}"))),
        ];

        IReadOnlyList<Step> result = ChatController.RemoveNonMatchingHistoricalTurnThinkingBlocks(turns, gpt);

        AssertThinkKept(result, "call-1-thinking");
        AssertThinkKept(result, "call-2-thinking");
    }

    private static ChatTurn AssistantTurn(long id, short modelId, params Step[] steps)
    {
        return new ChatTurn
        {
            Id = id,
            IsUser = false,
            ChatConfigSnapshot = new ChatConfigSnapshot
            {
                ModelSnapshot = new ModelSnapshot { ModelId = modelId }
            },
            Steps = steps
        };
    }

    private static ChatTurn UserTurn(long id)
    {
        return new ChatTurn
        {
            Id = id,
            IsUser = true,
            Steps =
            [
                new Step
                {
                    Id = id * 10,
                    ChatRoleId = (byte)DBChatRole.User,
                    StepContents = [StepContent.FromText($"user-{id}")]
                }
            ]
        };
    }

    private static Step AssistantStep(long id, string thinkText, params StepContent[] extraContents)
    {
        return new Step
        {
            Id = id,
            ChatRoleId = (byte)DBChatRole.Assistant,
            StepContents =
            [
                StepContent.FromThink(thinkText, $"sig-{thinkText}"),
                StepContent.FromText($"answer-{thinkText}"),
                .. extraContents
            ]
        };
    }

    private static Step ToolStep(long id, string toolCallId)
    {
        return new Step
        {
            Id = id,
            ChatRoleId = (byte)DBChatRole.ToolCall,
            StepContents = [StepContent.FromToolResponse(toolCallId, "tool-result")]
        };
    }

    private static void AssertThinkKept(IEnumerable<Step> steps, string thinkText)
    {
        Assert.Contains(steps.SelectMany(s => s.StepContents), c => c.TryGetThink(out string? text, out _) && text == thinkText);
    }

    private static void AssertThinkRemoved(IEnumerable<Step> steps, string thinkText)
    {
        Assert.DoesNotContain(steps.SelectMany(s => s.StepContents), c => c.TryGetThink(out string? text, out _) && text == thinkText);
        Assert.Contains(steps.SelectMany(s => s.StepContents), c => c.TryGetTextPart(out string? text) && text == $"answer-{thinkText}");
    }
}
