using System.Text;

namespace Chats.DockerInterface.Models;

public static class CommandExitEventFormatter
{
    public static string FormatForRunCommand(CommandExitEvent result)
    {
        string stdout = result.Stdout ?? string.Empty;
        string stderr = result.Stderr ?? string.Empty;

        bool isCleanSuccess = result.ExitCode == 0 && !result.IsTruncated && string.IsNullOrWhiteSpace(stderr);
        if (isCleanSuccess)
        {
            return string.IsNullOrWhiteSpace(stdout) ? "(no output)" : stdout;
        }

        StringBuilder sb = new();
        sb.AppendLine($"ExitCode: {result.ExitCode}");
        sb.AppendLine($"ExecutionTimeMs: {result.ExecutionTimeMs}");
        if (result.IsTruncated) sb.AppendLine("IsTruncated: true");

        if (!string.IsNullOrEmpty(stdout))
        {
            sb.AppendLine("Stdout:");
            sb.AppendLine(stdout);
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            sb.AppendLine("Stderr:");
            sb.AppendLine(stderr);
        }

        return sb.ToString().TrimEnd();
    }
}
