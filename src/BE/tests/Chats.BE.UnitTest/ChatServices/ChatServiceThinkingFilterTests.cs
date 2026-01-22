using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.UnitTest.ChatServices;

public class ChatServiceThinkingFilterTests
{
    [Fact]
    public void RemoveNonCurrentTurnThinkingBlocks_KeepsOnlyThinkingAfterLastUser()
    {
        // Arrange
        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("t1"),
                NeutralTextContent.Create("a1")
            ),
            NeutralMessage.FromUserText("u1"),
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("t2"),
                NeutralTextContent.Create("a2")
            ),
            NeutralMessage.FromUserText("u2")
        ];

        // Act
        IList<NeutralMessage> result = ChatService.RemoveNonCurrentTurnThinkingBlocks(messages);

        // Assert
        Assert.DoesNotContain(result[0].Contents, c => c is NeutralThinkContent);
        Assert.DoesNotContain(result[2].Contents, c => c is NeutralThinkContent);
    }

    [Fact]
    public void RemoveNonCurrentTurnThinkingBlocks_NoAssistant_NoChange()
    {
        // Arrange
        IList<NeutralMessage> messages = [NeutralMessage.FromUserText("hi")];

        // Act
        IList<NeutralMessage> result = ChatService.RemoveNonCurrentTurnThinkingBlocks(messages);

        // Assert
        Assert.Same(messages, result);
    }

    [Fact]
    public void RemoveNonCurrentTurnThinkingBlocks_SingleAssistant_NoChange()
    {
        // Arrange
        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("t"),
                NeutralTextContent.Create("a")
            )
        ];

        // Act
        IList<NeutralMessage> result = ChatService.RemoveNonCurrentTurnThinkingBlocks(messages);

        // Assert
        Assert.Same(messages, result);
        Assert.Contains(result[0].Contents, c => c is NeutralThinkContent);
    }

    [Fact]
    public void RemoveNonCurrentTurnThinkingBlocks_LastMessageIsUser_RemovesThinkingFromPreviousTurns()
    {
        // Arrange
        // Typical WebChat request before upstream call: conversation ends with the latest user message.
        // DeepSeek recommends clearing reasoning_content from previous turns.
        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("t1"),
                NeutralTextContent.Create("a1")
            ),
            NeutralMessage.FromUserText("u2")
        ];

        // Act
        IList<NeutralMessage> result = ChatService.RemoveNonCurrentTurnThinkingBlocks(messages);

        // Assert
        Assert.DoesNotContain(result[0].Contents, c => c is NeutralThinkContent);
    }

    [Fact]
    public void RemoveNonCurrentTurnThinkingBlocks_InterleavedToolCallsAfterLastUser_Kept()
    {
        // Arrange
        // Simulate a multi-subrequest tool-call loop within the same user turn.
        // Messages after the last user message belong to the current turn and MUST keep thinking.
        IList<NeutralMessage> messages =
        [
            // Previous turn assistant (should be cleared)
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("old-think"),
                NeutralTextContent.Create("old-answer")
            ),
            NeutralMessage.FromUserText("old-user"),

            // Current turn begins here
            NeutralMessage.FromUserText("current-user"),

            // Assistant tool call 1 (must keep thinking)
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("t-call-1"),
                NeutralToolCallContent.Create("call_1", "create_docker_session", "{}")
            ),
            // Tool response
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", "sessionId: xxx")
            ),

            // Assistant tool call 2 (must keep thinking)
            NeutralMessage.FromAssistant(
                NeutralThinkContent.Create("t-call-2"),
                NeutralToolCallContent.Create("call_2", "run_command", "{\"sessionId\":\"xxx\"}")
            ),
        ];

        // Act
        IList<NeutralMessage> result = ChatService.RemoveNonCurrentTurnThinkingBlocks(messages);

        // Assert
        Assert.DoesNotContain(result[0].Contents, c => c is NeutralThinkContent);
        Assert.Contains(result[3].Contents, c => c is NeutralThinkContent);
        Assert.Contains(result[5].Contents, c => c is NeutralThinkContent);
    }
}
