using System.Text;

namespace Chats.DockerInterface.Models;

/// <summary>
/// 用于 run_command：基于 CommandExitEvent 生成与旧 run_command 相同的 summary 文本。
///
/// 注意：
/// - ExecuteCommandStreamAsync 不负责生成 summary；只负责产出原始 stdout/stderr chunk 与原始 exit 事件。
/// - ExecuteCommandAsync 仍会按 OutputOptions 截断并设置 IsTruncated（与旧 CommandResult 行为一致）。
/// </summary>
public sealed class CommandStreamSummaryBuilder(OutputOptions options)
{
    public CommandExitEvent ApplyTruncationIfNeeded(CommandExitEvent exit)
    {
        if (exit.IsTruncated)
        {
            // Assume stdout/stderr already truncated (ExecuteCommandAsync behavior).
            return exit;
        }

        (string truncatedStdout, bool stdoutTruncated) = CommandOutputTruncation.Truncate(exit.Stdout ?? string.Empty, options);
        (string truncatedStderr, bool stderrTruncated) = CommandOutputTruncation.Truncate(exit.Stderr ?? string.Empty, options);

        bool isTruncated = stdoutTruncated || stderrTruncated;
        if (!isTruncated)
        {
            return exit;
        }

        return new CommandExitEvent(
            truncatedStdout,
            truncatedStderr,
            exit.ExitCode,
            exit.ExecutionTimeMs,
            IsTruncated: true);
    }

    public string BuildRunCommandSummary(CommandExitEvent exit)
    {
        CommandExitEvent truncated = ApplyTruncationIfNeeded(exit);
        return CommandExitEventFormatter.FormatForRunCommand(truncated);
    }
}
