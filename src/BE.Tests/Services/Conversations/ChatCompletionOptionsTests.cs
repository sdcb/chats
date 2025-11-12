using OpenAI.Chat;

namespace Chats.BE.Tests.Services.Conversations;

public class ChatCompletionOptionsTests
{
    [Fact]
    public void IsSearchEnabled_ReturnsTrue_WhenEnableSearchIsTrue()
    {
        // Arrange
        ChatCompletionOptions options = new();
        options.Patch.Set("$.enable_search"u8, true);

        // Act
        bool result = IsSearchEnabled(options);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSearchEnabled_ReturnsFalse_WhenEnableSearchIsFalse()
    {
        // Arrange
        ChatCompletionOptions options = new();
        options.Patch.Set("$.enable_search"u8, false);

        // Act
        bool result = IsSearchEnabled(options);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSearchEnabled_ReturnsFalse_WhenEnableSearchIsNotPresent()
    {
        // Arrange
        ChatCompletionOptions options = new();

        // Act
        bool result = IsSearchEnabled(options);

        // Assert
        Assert.False(result);
    }

    static bool IsSearchEnabled(ChatCompletionOptions options)
    {
        return options.Patch.TryGetValue("$.enable_search"u8, out bool enableSearch) && enableSearch;
    }
}
