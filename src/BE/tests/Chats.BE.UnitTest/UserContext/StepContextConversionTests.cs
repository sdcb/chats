using Chats.BE.DB.Init;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using Chats.BE.Services.UserContext;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.UserContext;

public sealed class StepContextConversionTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 8, 8, 14, 30, 0, TimeSpan.FromHours(8));

    [Fact]
    public void ToNeutral_WithTargetSpan_ShouldRenderTemplateAndMovePrimaryTextFirst()
    {
        StepContent text = StepContent.FromText(
            "hello",
            UserContextTemplate.Build(FixedTime,
            [
                new("code_interpreter", "docker-a", [1]),
            ]));
        Step step = new()
        {
            ChatRoleId = (byte)DBChatRole.User,
            CreatedAt = DateTime.UtcNow,
            StepContents =
            [
                StepContent.FromFileUrl("https://example.test/file.png"),
                text,
            ],
        };

        NeutralMessage spanOne = step.ToNeutral(targetSpanId: 1);
        NeutralTextContent renderedText = Assert.IsType<NeutralTextContent>(spanOne.Contents[0]);
        Assert.Contains("<code_interpreter>\ndocker-a\n</code_interpreter>", renderedText.Content);
        Assert.EndsWith("<user_request>hello</user_request>", renderedText.Content);
        Assert.IsType<NeutralFileUrlContent>(spanOne.Contents[1]);

        NeutralMessage spanTwo = step.ToNeutral(targetSpanId: 2);
        NeutralTextContent otherSpanText = Assert.IsType<NeutralTextContent>(spanTwo.Contents[0]);
        Assert.DoesNotContain("code_interpreter", otherSpanText.Content);
        Assert.EndsWith("<user_request>hello</user_request>", otherSpanText.Content);
    }

    [Fact]
    public void ToNeutral_WithoutTargetSpan_ShouldExposeRawContentAndOriginalOrder()
    {
        Step step = new()
        {
            ChatRoleId = (byte)DBChatRole.User,
            CreatedAt = DateTime.UtcNow,
            StepContents =
            [
                StepContent.FromFileUrl("https://example.test/file.png"),
                StepContent.FromText("hello", UserContextTemplate.Build(FixedTime)),
            ],
        };

        NeutralMessage message = step.ToNeutral();

        Assert.IsType<NeutralFileUrlContent>(message.Contents[0]);
        Assert.Equal("hello", Assert.IsType<NeutralTextContent>(message.Contents[1]).Content);
    }

    [Fact]
    public void Clone_ShouldPreserveContextTemplate()
    {
        StepContent original = StepContent.FromText("hello", "before {{USER_CONTENT}} after");

        StepContent clone = original.Clone();

        Assert.Equal(original.StepContentText!.Content, clone.StepContentText!.Content);
        Assert.Equal(original.StepContentText.ContextTemplate, clone.StepContentText.ContextTemplate);
    }

    [Fact]
    public void DefaultPrompt_ShouldNotContainDynamicTimeVariables()
    {
        Assert.DoesNotContain("{{CURRENT_DATE}}", InitService.DefaultPrompt);
        Assert.DoesNotContain("{{CURRENT_TIME}}", InitService.DefaultPrompt);
    }
}
