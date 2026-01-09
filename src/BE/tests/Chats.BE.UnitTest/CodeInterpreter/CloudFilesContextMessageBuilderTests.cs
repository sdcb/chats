using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DB.Enums;
using DBFile = Chats.DB.File;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CloudFilesContextMessageBuilderTests
{
    private static Step UserTextStep(string text)
        => new()
        {
            ChatRoleId = (byte)DBChatRole.User,
            CreatedAt = DateTime.UtcNow,
            StepContents = [StepContent.FromText(text)],
        };

    private static Step AssistantWithFileStep(DBFile file)
        => new()
        {
            ChatRoleId = (byte)DBChatRole.Assistant,
            CreatedAt = DateTime.UtcNow,
            StepContents = [StepContent.FromFile(file)],
        };

    private static string? PrefixIfAnyFile(IEnumerable<Step> steps)
    {
        bool hasAnyFile = steps
            .SelectMany(s => s.StepContents ?? [])
            .Any(sc => sc.StepContentFile?.File != null);
        return hasAnyFile ? "[Cloud Files Available]\n- any\n" : null;
    }

    [Fact]
    public void BuildMessages_CodeExecutionDisabled_ShouldNotInjectContext()
    {
        Step current = UserTextStep("hello");

        IList<NeutralMessage> msgs = CloudFilesContextMessageBuilder.BuildMessages(
            historySteps: [],
            currentRoundSteps: [current],
            codeExecutionEnabled: false,
            buildCloudFilesContextPrefix: _ => throw new InvalidOperationException("should not be called"));

        NeutralTextContent content = Assert.IsType<NeutralTextContent>(msgs.Single().Contents.Single());
        Assert.Equal("hello", content.Content);
    }

    [Fact]
    public void BuildMessages_CodeExecutionEnabled_NoFiles_ShouldNotInjectContext()
    {
        Step current = UserTextStep("hello");

        IList<NeutralMessage> msgs = CloudFilesContextMessageBuilder.BuildMessages(
            historySteps: [],
            currentRoundSteps: [current],
            codeExecutionEnabled: true,
            buildCloudFilesContextPrefix: PrefixIfAnyFile);

        NeutralTextContent content = Assert.IsType<NeutralTextContent>(msgs.Single().Contents.Single());
        Assert.Equal("hello", content.Content);
    }

    [Fact]
    public void BuildMessages_CodeExecutionEnabled_WithFiles_ShouldOnlyInjectIntoCurrentRoundUserPrompt()
    {
        DBFile file = new()
        {
            Id = 1,
            FileName = "a.txt",
            StorageKey = "a.txt",
            Size = 1,
            MediaType = "text/plain",
            FileServiceId = 1,
            FileService = null!,
            ClientInfoId = 1,
            CreateUserId = 1,
            CreatedAt = DateTime.UtcNow,
            ClientInfo = null!,
            CreateUser = null!,
        };

        Step historyFile = AssistantWithFileStep(file);
        Step historyUser = UserTextStep("old user after file");
        Step currentUser = UserTextStep("current user");

        IList<NeutralMessage> msgs = CloudFilesContextMessageBuilder.BuildMessages(
            historySteps: [historyFile, historyUser],
            currentRoundSteps: [currentUser],
            codeExecutionEnabled: true,
            buildCloudFilesContextPrefix: PrefixIfAnyFile);

        // history user should NOT be injected
        NeutralTextContent historyUserText = Assert.IsType<NeutralTextContent>(msgs[1].Contents.Single());
        Assert.Equal("old user after file", historyUserText.Content);

        // current user SHOULD be injected
        Assert.Equal(2, msgs[2].Contents.Count);
        NeutralTextContent injectedPrefix = Assert.IsType<NeutralTextContent>(msgs[2].Contents[0]);
        NeutralTextContent injectedUserText = Assert.IsType<NeutralTextContent>(msgs[2].Contents[1]);
        Assert.StartsWith("[Cloud Files Available]", injectedPrefix.Content);
        Assert.Equal("current user", injectedUserText.Content);
    }
}
