using Chats.DockerInterface.Models;

namespace Chats.BE.UnitTest.Docker;

public sealed class ExecuteCommandStreamAsyncTests
{
    [Fact]
    public void StreamSummary_LastEventExit_OutputIsCleanSuccess_WhenNoTruncationNoStderr()
    {
        OutputOptions options = new()
        {
            MaxOutputBytes = 1024,
            Strategy = TruncationStrategy.HeadAndTail,
            TruncationMessage = "<TRUNC {0}>"
        };

        CommandStreamSummaryBuilder b = new(options);
        CommandExitEvent exit = new(
            Stdout: "hello\n",
            Stderr: string.Empty,
            ExitCode: 0,
            ExecutionTimeMs: 12,
            IsTruncated: false);

        string output = b.BuildRunCommandSummary(exit);
        Assert.Equal("hello\n", output);
    }

    [Fact]
    public void StreamSummary_LastEventExit_OutputIncludesTruncation_AndMatchesStreamedContent()
    {
        OutputOptions options = new()
        {
            MaxOutputBytes = 40,
            Strategy = TruncationStrategy.HeadAndTail,
            TruncationMessage = "\n(... {0} bytes truncated)\n"
        };

        string fullStdout = "HEAD-" + new string('A', 120) + "-TAIL";
        string fullStderr = "ERR-" + new string('B', 120) + "-END";

        CommandStreamSummaryBuilder b = new(options);
        CommandExitEvent exit = new(
            Stdout: fullStdout,
            Stderr: fullStderr,
            ExitCode: 2,
            ExecutionTimeMs: 999,
            IsTruncated: false);

        string output = b.BuildRunCommandSummary(exit);

        Assert.Contains("ExitCode: 2", output);
        Assert.Contains("ExecutionTimeMs: 999", output);
        Assert.Contains("IsTruncated: true", output);
        Assert.Contains("Stdout:", output);
        Assert.Contains("Stderr:", output);
        Assert.Contains("bytes truncated", output);

        // The truncated output must still be derived from the full streamed content.
        Assert.Contains("HEAD-", output);
        Assert.Contains("-TAIL", output);
        Assert.Contains("ERR-", output);
        Assert.Contains("-END", output);
    }
}
