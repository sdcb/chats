using Chats.BE.Services.UserContext;

namespace Chats.BE.UnitTest.UserContext;

public sealed class UserContextTemplateTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 8, 8, 14, 30, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Render_NullTemplate_ShouldReturnContentUnchanged()
    {
        string content = "hello\r\n{{#spans:1}}\nworld";

        string rendered = UserContextTemplate.Render(null, content, spanId: 1);

        Assert.Equal(content, rendered);
    }

    [Fact]
    public void Build_ShouldNormalizeAndMergeEquivalentContributions()
    {
        string template = UserContextTemplate.Build(FixedTime,
        [
            new("code_interpreter", "docker-a\r\nfile.txt\r\n", [3, 1]),
            new("code_interpreter", "docker-a\nfile.txt", [2, 1]),
            new("empty", "  \r\n", [1]),
        ]);

        Assert.Contains("<current_time>2026-08-08T14:30:00+08:00</current_time>", template);
        Assert.Contains("{{#spans:1,2,3}}\n<code_interpreter>\ndocker-a\nfile.txt\n</code_interpreter>\n{{/spans}}", template);
        Assert.DoesNotContain("<empty>", template);
        Assert.DoesNotContain('\r', template);
    }

    [Fact]
    public void Build_ModelContributions_ShouldMergeSpansUsingTheSameModel()
    {
        string template = UserContextTemplate.Build(FixedTime,
        [
            new("model", "gpt-shared", [2]),
            new("model", "gpt-other", [3]),
            new("model", "gpt-shared", [1]),
        ]);

        Assert.Contains("{{#spans:1,2}}\n<model>\ngpt-shared\n</model>\n{{/spans}}", template);
        Assert.Contains("{{#spans:3}}\n<model>\ngpt-other\n</model>\n{{/spans}}", template);

        string spanTwo = UserContextTemplate.Render(template, "hello", spanId: 2);
        Assert.Contains("<model>\ngpt-shared\n</model>", spanTwo);
        Assert.DoesNotContain("gpt-other", spanTwo);
    }

    [Fact]
    public void Render_ShouldIncludeOnlyMatchingSpanBlocksWithoutBlankLines()
    {
        string template = UserContextTemplate.Build(FixedTime,
        [
            new("code_interpreter", "shared", [1, 2]),
            new("other_context", "span three", [3]),
        ]);

        string rendered = UserContextTemplate.Render(template, "hello", spanId: 2);

        Assert.Equal("""
            <context>
            <current_time>2026-08-08T14:30:00+08:00</current_time>
            <code_interpreter>
            shared
            </code_interpreter>
            </context>
            <user_request>hello</user_request>
            """.ReplaceLineEndings("\n"), rendered);
    }

    [Fact]
    public void Render_SecondSpanOnly_ShouldRemoveFirstSpanBlockWithoutLeavingBlankLines()
    {
        string template = UserContextTemplate.Build(FixedTime,
        [
            new("first_span_context", "only span one", [1]),
            new("second_span_context", "only span two", [2]),
        ]);

        string rendered = UserContextTemplate.Render(template, "hello", spanId: 2);

        Assert.Equal("""
            <context>
            <current_time>2026-08-08T14:30:00+08:00</current_time>
            <second_span_context>
            only span two
            </second_span_context>
            </context>
            <user_request>hello</user_request>
            """.ReplaceLineEndings("\n"), rendered);
        Assert.DoesNotContain("\n\n", rendered);
    }

    [Fact]
    public void Render_ShouldNotInterpretTemplateSyntaxInsideUserContent()
    {
        string template = UserContextTemplate.Build(FixedTime,
        [
            new("code_interpreter", "secret", [1]),
        ]);
        string content = "a\n{{#spans:1}}\n{{USER_CONTENT}}\n{{/spans}}\n<b>c</b>";

        string rendered = UserContextTemplate.Render(template, content, spanId: 0);

        Assert.DoesNotContain("secret", rendered);
        Assert.EndsWith($"<user_request>{content}</user_request>", rendered);
    }

    [Theory]
    [InlineData("<user_request>none</user_request>")]
    [InlineData("{{USER_CONTENT}}{{USER_CONTENT}}")]
    [InlineData("{{#spans:}}\n{{USER_CONTENT}}\n{{/spans}}")]
    [InlineData("{{#spans:256}}\n{{USER_CONTENT}}\n{{/spans}}")]
    [InlineData("{{#spans:1,1}}\n{{USER_CONTENT}}\n{{/spans}}")]
    [InlineData("{{/spans}}\n{{USER_CONTENT}}")]
    [InlineData("{{#spans:1}}\n{{USER_CONTENT}}")]
    [InlineData("{{#spans:1}}\n{{#spans:1}}\n{{USER_CONTENT}}\n{{/spans}}\n{{/spans}}")]
    public void Render_InvalidTemplate_ShouldThrow(string template)
    {
        Assert.Throws<InvalidOperationException>(() => UserContextTemplate.Render(template, "hello", spanId: 1));
    }

    [Fact]
    public void Render_InlineDirectiveLikeText_ShouldRemainLiteral()
    {
        string template = "before {{#spans:1}}\n{{USER_CONTENT}}\nafter {{/spans}}";

        string rendered = UserContextTemplate.Render(template, "hello", spanId: 0);

        Assert.Equal("before {{#spans:1}}\nhello\nafter {{/spans}}", rendered);
    }

    [Fact]
    public void Build_InvalidContributionKey_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => UserContextTemplate.Build(FixedTime,
        [
            new("1invalid", "content", [1]),
        ]));
    }
}
