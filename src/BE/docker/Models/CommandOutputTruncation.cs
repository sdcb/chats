using System.Text;

namespace Chats.DockerInterface.Models;

public static class CommandOutputTruncation
{
    public static (string output, bool truncated) Truncate(string output, OutputOptions options)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(output ?? string.Empty);

        if (bytes.Length <= options.MaxOutputBytes)
        {
            return (output ?? string.Empty, false);
        }

        int halfSize = options.MaxOutputBytes / 2;

        string truncatedOutput;
        switch (options.Strategy)
        {
            case TruncationStrategy.Head:
                truncatedOutput = Encoding.UTF8.GetString(bytes, 0, options.MaxOutputBytes);
                break;
            case TruncationStrategy.Tail:
                truncatedOutput = Encoding.UTF8.GetString(bytes, bytes.Length - options.MaxOutputBytes, options.MaxOutputBytes);
                break;
            case TruncationStrategy.HeadAndTail:
                truncatedOutput = Encoding.UTF8.GetString(bytes, 0, halfSize) +
                                Encoding.UTF8.GetString(bytes, bytes.Length - halfSize, halfSize);
                break;
            default:
                return (output ?? string.Empty, false);
        }

        // Calculate omitted lines
        int totalLines = CountLines(output ?? string.Empty);
        int keptLines = CountLines(truncatedOutput);
        int omittedLines = Math.Max(0, totalLines - keptLines);

        return options.Strategy switch
        {
            TruncationStrategy.Head => (
                truncatedOutput + string.Format(options.TruncationMessage, omittedLines),
                true),

            TruncationStrategy.Tail => (
                string.Format(options.TruncationMessage, omittedLines) + truncatedOutput,
                true),

            TruncationStrategy.HeadAndTail => (
                Encoding.UTF8.GetString(bytes, 0, halfSize) +
                string.Format(options.TruncationMessage, omittedLines) +
                Encoding.UTF8.GetString(bytes, bytes.Length - halfSize, halfSize),
                true),

            _ => (output ?? string.Empty, false)
        };
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Split(["\r\n", "\n"], StringSplitOptions.None).Length;
    }
}
