using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.AgentRuns;
using Chats.DB;
using Chats.DB.Enums;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.Services.AgentRuns;

public class AskQuestionToolTests
{
    [Fact]
    public void TryParse_ValidQuestions_AssignsMissingAndDuplicateIds()
    {
        string args = """
            {"questions":[
              {"id":"db","question":"Which database?","type":"single","options":["SQL Server","SQLite","SQL Server"]},
              {"id":"db","question":"Anything else?","type":"text"},
              {"question":"Targets?","type":"MULTI","options":["web","api"],"default":"web"}
            ]}
            """;

        Assert.True(AskQuestionTool.TryParse(args, out AskQuestionItem[] questions, out string? error), error);
        Assert.Equal(["db", "q2", "q3"], questions.Select(x => x.Id).ToArray());
        Assert.Equal(["SQL Server", "SQLite"], questions[0].Options);
        Assert.Empty(questions[1].Options);
        Assert.Equal("multi", questions[2].Type);
        Assert.Equal("web", questions[2].Default);
    }

    [Fact]
    public void TryParse_StringifiedQuestions_IsAccepted()
    {
        string args = """{"questions":"[{\"question\":\"Name?\",\"type\":\"text\"}]"}""";

        Assert.True(AskQuestionTool.TryParse(args, out AskQuestionItem[] questions, out _));
        Assert.Equal("Name?", Assert.Single(questions).Question);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"questions":[]}""")]
    [InlineData("""{"questions":[{"question":"a"},{"question":"b"},{"question":"c"},{"question":"d"}]}""")]
    [InlineData("""{"questions":[{"question":"Pick","type":"single"}]}""")]
    [InlineData("""{"questions":[{"question":"Pick","type":"dropdown","options":["a"]}]}""")]
    [InlineData("""{"questions":[{"type":"text"}]}""")]
    public void TryParse_Invalid_ReturnsError(string args)
    {
        Assert.False(AskQuestionTool.TryParse(args, out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void FormatAnswers_MapsAnswersByQuestionType()
    {
        AskQuestionItem[] questions =
        [
            new() { Id = "q1", Question = "数据库？", Type = "single", Options = ["A", "B"] },
            new() { Id = "q2", Question = "Targets?", Type = "multi", Options = ["web", "api"] },
            new() { Id = "q3", Question = "Notes?", Type = "text" },
        ];

        string json = AskQuestionTool.FormatAnswers(questions,
        [
            new() { Id = "q1", Values = ["B"] },
            new() { Id = "q2", Values = ["web", "api"] },
        ]);

        Assert.Contains("数据库", json); // relaxed escaping keeps CJK readable for the model
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("answered", root["status"]!.GetValue<string>());
        JsonArray answers = root["answers"]!.AsArray();
        Assert.Equal("B", answers[0]!["answer"]!.GetValue<string>());
        Assert.Equal(["web", "api"], answers[1]!["answer"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Null(answers[2]!["answer"]);
    }
}

public class LiveRunTests
{
    [Fact]
    public async Task ReadAfter_ReturnsOnlyNewEvents_AndSignalsChanges()
    {
        LiveRun run = new(1, 1, 1);
        Assert.Equal(1, run.Append("{\"a\":1}"));
        Assert.Equal(2, run.Append("{\"a\":2}"));

        (string[] events, int firstSeq, bool completed, Task changed) = run.Read(1);
        Assert.Equal(["{\"a\":2}"], events);
        Assert.Equal(2, firstSeq);
        Assert.False(completed);
        Assert.False(changed.IsCompleted);

        run.Append("{\"a\":3}");
        await changed.WaitAsync(TimeSpan.FromSeconds(1));

        run.MarkCompleted();
        (events, firstSeq, completed, _) = run.Read(2);
        Assert.Equal(["{\"a\":3}"], events);
        Assert.Equal(3, firstSeq);
        Assert.True(completed);
        Assert.Throws<InvalidOperationException>(() => run.Append("{}"));
    }

    [Fact]
    public void PeekUnpersisted_StartsAfterPersistedSeq()
    {
        LiveRun run = new(1, 1, 1);
        run.Append("1");
        run.Append("2");
        run.Append("3");
        run.PersistedSeq = 2;

        (int firstSeq, string[] events) = run.PeekUnpersisted();
        Assert.Equal(3, firstSeq);
        Assert.Equal(["3"], events);
    }
}

public class PendingAskQuestionTests
{
    [Fact]
    public void GetPendingAskQuestionCalls_IgnoresAnsweredCalls()
    {
        Step call = new()
        {
            Id = 1,
            ChatRoleId = (byte)DBChatRole.Assistant,
            StepContents =
            [
                ToolCall("call_1", AskQuestionTool.Name),
                ToolCall("call_2", AskQuestionTool.Name),
                ToolCall("call_3", "run_command"),
            ],
        };
        Step response = new()
        {
            Id = 2,
            ChatRoleId = (byte)DBChatRole.ToolCall,
            StepContents =
            [
                new StepContent
                {
                    ContentTypeId = (byte)DBStepContentType.ToolCallResponse,
                    StepContentToolCallResponse = new StepContentToolCallResponse { ToolCallId = "call_1", Response = "{}" },
                },
            ],
        };

        StepContentToolCall pending = Assert.Single(ChatController.GetPendingAskQuestionCalls([response, call]));
        Assert.Equal("call_2", pending.ToolCallId);
    }

    private static StepContent ToolCall(string id, string name) => new()
    {
        ContentTypeId = (byte)DBStepContentType.ToolCall,
        StepContentToolCall = new StepContentToolCall { ToolCallId = id, Name = name, Parameters = "{}" },
    };
}
