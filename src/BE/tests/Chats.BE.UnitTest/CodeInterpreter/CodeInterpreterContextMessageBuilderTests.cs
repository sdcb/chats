using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterContextMessageBuilderTests
{
    private static Step UserTextStep(string text)
        => new()
        {
            ChatRoleId = (byte)DBChatRole.User,
            CreatedAt = DateTime.UtcNow,
            StepContents = [StepContent.FromText(text)],
        };

    [Fact]
    public void BuildMessages_CodeExecutionDisabled_ShouldNotInjectContext()
    {
        Step current = UserTextStep("hello");

        IList<NeutralMessage> msgs = CodeInterpreterContextMessageBuilder.BuildMessages(
            historySteps: [],
            currentRoundSteps: [current],
            codeExecutionEnabled: false,
            contextPrefix: "prefix");

        NeutralTextContent content = Assert.IsType<NeutralTextContent>(msgs.Single().Contents.Single());
        Assert.Equal("hello", content.Content);
    }

    [Fact]
    public void BuildMessages_CodeExecutionEnabled_NoPrefix_ShouldNotInjectContext()
    {
        Step current = UserTextStep("hello");

        IList<NeutralMessage> msgs = CodeInterpreterContextMessageBuilder.BuildMessages(
            historySteps: [],
            currentRoundSteps: [current],
            codeExecutionEnabled: true,
            contextPrefix: null);

        NeutralTextContent content = Assert.IsType<NeutralTextContent>(msgs.Single().Contents.Single());
        Assert.Equal("hello", content.Content);
    }

    [Fact]
    public void BuildMessages_CodeExecutionEnabled_WithPrefix_ShouldOnlyInjectIntoCurrentRoundUserPrompt()
    {
        Step historyUser = UserTextStep("old user");
        Step currentUser = UserTextStep("current user");

        IList<NeutralMessage> msgs = CodeInterpreterContextMessageBuilder.BuildMessages(
            historySteps: [historyUser],
            currentRoundSteps: [currentUser],
            codeExecutionEnabled: true,
            contextPrefix: "[CTX]\n");

        // history user should NOT be injected
        NeutralTextContent historyUserText = Assert.IsType<NeutralTextContent>(msgs[0].Contents.Single());
        Assert.Equal("old user", historyUserText.Content);

        // current user SHOULD be injected
        Assert.Equal(2, msgs[1].Contents.Count);
        NeutralTextContent injectedPrefix = Assert.IsType<NeutralTextContent>(msgs[1].Contents[0]);
        NeutralTextContent injectedUserText = Assert.IsType<NeutralTextContent>(msgs[1].Contents[1]);
        Assert.StartsWith("[CTX]", injectedPrefix.Content);
        Assert.Equal("current user", injectedUserText.Content);
    }
}
