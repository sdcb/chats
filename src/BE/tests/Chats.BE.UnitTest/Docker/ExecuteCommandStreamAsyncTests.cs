using Chats.DockerInterface.Models;

namespace Chats.BE.UnitTest.Docker;

public sealed class ExecuteCommandStreamAsyncTests
{
    [Fact]
    public void StreamExit_BuildExit_IsCleanSuccess_WhenNoTruncationNoStderr()
    {
        OutputOptions options = new()
        {
            MaxOutputBytes = 1024,
            Strategy = TruncationStrategy.HeadAndTail,
            TruncationMessage = "<TRUNC {0}>"
        };

        CommandStreamSummaryBuilder b = new(options);
        b.AppendStdout("hello\n");

        CommandExitEvent exit = b.BuildExit(exitCode: 0, executionTimeMs: 12);
        Assert.Equal("hello\n", exit.Stdout);
        Assert.Equal(string.Empty, exit.Stderr);
        Assert.False(exit.IsTruncated);

        string output = CommandExitEventFormatter.FormatForRunCommand(exit);
        Assert.Equal("hello\n", output);
    }

    [Fact]
    public void StreamExit_BuildExit_TruncatesAndMarksIsTruncated_AndFormatterIncludesMetadata()
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
        b.AppendStdout(fullStdout);
        b.AppendStderr(fullStderr);

        CommandExitEvent exit = b.BuildExit(exitCode: 2, executionTimeMs: 999);
        (string expectedStdout, bool stdoutTruncated) = CommandOutputTruncation.Truncate(fullStdout, options);
        (string expectedStderr, bool stderrTruncated) = CommandOutputTruncation.Truncate(fullStderr, options);

        Assert.Equal(expectedStdout, exit.Stdout);
        Assert.Equal(expectedStderr, exit.Stderr);
        Assert.Equal(stdoutTruncated || stderrTruncated, exit.IsTruncated);
        Assert.True(exit.IsTruncated);

        string output = CommandExitEventFormatter.FormatForRunCommand(exit);
        Assert.Contains("exit code: 2", output);
        Assert.Contains("execution time: 999ms", output);
        Assert.Contains("truncated", output);
        Assert.Contains("Stdout:", output);
        Assert.Contains("Stderr:", output);
        Assert.Contains("bytes truncated", output);
    }
}
