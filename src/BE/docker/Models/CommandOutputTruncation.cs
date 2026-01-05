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
        int omittedBytes = bytes.Length - options.MaxOutputBytes;

        return options.Strategy switch
        {
            TruncationStrategy.Head => (
                Encoding.UTF8.GetString(bytes, 0, options.MaxOutputBytes) +
                string.Format(options.TruncationMessage, omittedBytes),
                true),

            TruncationStrategy.Tail => (
                string.Format(options.TruncationMessage, omittedBytes) +
                Encoding.UTF8.GetString(bytes, bytes.Length - options.MaxOutputBytes, options.MaxOutputBytes),
                true),

            TruncationStrategy.HeadAndTail => (
                Encoding.UTF8.GetString(bytes, 0, halfSize) +
                string.Format(options.TruncationMessage, omittedBytes) +
                Encoding.UTF8.GetString(bytes, bytes.Length - halfSize, halfSize),
                true),

            _ => (output ?? string.Empty, false)
        };
    }
}
