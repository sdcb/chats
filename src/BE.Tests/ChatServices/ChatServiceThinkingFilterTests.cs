using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Tests.ChatServices;

public class ChatServiceThinkingFilterTests
{
    [Fact]
    public void RemoveNonCurrentTurnThinkingBlocks_KeepsOnlyLastAssistantThinking()
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
        Assert.Contains(result[2].Contents, c => c is NeutralThinkContent);
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
}
