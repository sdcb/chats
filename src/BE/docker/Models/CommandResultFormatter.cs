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
        string metadataInfo = $"exit code: {result.ExitCode}, execution time: {result.ExecutionTimeMs}ms";
        if (result.IsTruncated)
        {
            metadataInfo += ", truncated";
        }
        sb.AppendLine(metadataInfo);

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
