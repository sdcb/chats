using System.Text;

namespace Chats.DockerInterface.Models;

/// <summary>
/// 用于 ExecuteCommandStreamAsync：缓存完整 stdout/stderr（progress 阶段不截断），结束时按 OutputOptions 截断，
/// 生成与 ExecuteCommandAsync 返回完全一致字段的 CommandExitEvent。
/// </summary>
internal sealed class CommandStreamSummaryBuilder(OutputOptions options)
{
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();

    public void AppendStdout(string chunk) => _stdout.Append(chunk);

    public void AppendStderr(string chunk) => _stderr.Append(chunk);

    public CommandExitEvent BuildExit(long exitCode, long executionTimeMs)
    {
        (string truncatedStdout, bool stdoutTruncated) = CommandOutputTruncation.Truncate(_stdout.ToString(), options);
        (string truncatedStderr, bool stderrTruncated) = CommandOutputTruncation.Truncate(_stderr.ToString(), options);

        return new CommandExitEvent(
            truncatedStdout,
            truncatedStderr,
            exitCode,
            executionTimeMs,
            IsTruncated: stdoutTruncated || stderrTruncated);
    }
}
