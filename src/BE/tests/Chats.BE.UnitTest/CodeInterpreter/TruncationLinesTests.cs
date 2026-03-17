using Chats.DockerInterface.Models;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class TruncationLinesTests
{
    [Fact]
    public void CommandOutputTruncation_ShowsLinesNotBytes()
    {
        // Arrange
        string content = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Output line {i}"));
        
        OutputOptions options = new()
        {
            MaxOutputBytes = 100,
            Strategy = TruncationStrategy.HeadAndTail,
        };

        // Act
        (string truncatedOutput, bool truncated) = CommandOutputTruncation.Truncate(content, options);

        // Assert
        Assert.True(truncated);
        Assert.Contains("lines omitted", truncatedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("bytes omitted", truncatedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandOutputTruncation_CountsLinesCorrectly()
    {
        // Arrange: Create content with exactly 10 lines
        string content = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"Line {i}"));
        
        OutputOptions options = new()
        {
            MaxOutputBytes = 30, // Very small to force aggressive truncation
            Strategy = TruncationStrategy.HeadAndTail,
        };

        // Act
        (string truncatedOutput, bool truncated) = CommandOutputTruncation.Truncate(content, options);

        // Assert
        Assert.True(truncated);
        
        // Extract and verify the omitted lines count
        int omittedIdx = truncatedOutput.IndexOf("lines omitted", StringComparison.Ordinal);
        Assert.True(omittedIdx > 0, "Should contain 'lines omitted' message");
        
        // The message format is: "\n... [Output truncated: X lines omitted] ...\n"
        int startIdx = truncatedOutput.LastIndexOf('[', omittedIdx);
        int colonIdx = truncatedOutput.IndexOf(':', startIdx);
        string numberPart = truncatedOutput[(colonIdx + 1)..omittedIdx].Trim();
        
        Assert.True(int.TryParse(numberPart, out int omittedLines));
        Assert.True(omittedLines > 0 && omittedLines <= 10, $"Omitted lines should be between 1-10, got {omittedLines}");
    }
}
