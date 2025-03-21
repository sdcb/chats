using Chats.BE.DB;

namespace Chats.BE.Tests.Services;

public class ChatConfigHashTests
{
    [Fact]
    public void GenerateDBHashCode_ShouldGenerateConsistentHash_ForIdenticalObjects()
    {
        // Arrange
        ChatConfig config1 = new()
        {
            ModelId = 1,
            SystemPrompt = "Hello, world!",
            Temperature = 0.5f,
            WebSearchEnabled = true,
            MaxOutputTokens = 100,
            ReasoningEffort = 2
        };

        ChatConfig config2 = new()
        {
            ModelId = 1,
            SystemPrompt = "Hello, world!",
            Temperature = 0.5f,
            WebSearchEnabled = true,
            MaxOutputTokens = 100,
            ReasoningEffort = 2
        };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentModelId()
    {
        // Arrange
        ChatConfig config1 = new() { ModelId = 1 };
        ChatConfig config2 = new() { ModelId = 2 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentSystemPrompt()
    {
        // Arrange
        ChatConfig config1 = new() { SystemPrompt = "Prompt A" };
        ChatConfig config2 = new() { SystemPrompt = "Prompt B" };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldHandleNullSystemPrompt()
    {
        // Arrange
        ChatConfig config1 = new() { SystemPrompt = null };
        ChatConfig config2 = new() { SystemPrompt = string.Empty };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentTemperature()
    {
        // Arrange
        ChatConfig config1 = new() { Temperature = 0.5f };
        ChatConfig config2 = new() { Temperature = 0.6f };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldHandleNullTemperature()
    {
        // Arrange
        ChatConfig config1 = new() { Temperature = null };
        ChatConfig config2 = new() { Temperature = 0.5f };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentWebSearchEnabled()
    {
        // Arrange
        ChatConfig config1 = new() { WebSearchEnabled = true };
        ChatConfig config2 = new() { WebSearchEnabled = false };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentMaxOutputTokens()
    {
        // Arrange
        ChatConfig config1 = new() { MaxOutputTokens = 100 };
        ChatConfig config2 = new() { MaxOutputTokens = 200 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldHandleNullMaxOutputTokens()
    {
        // Arrange
        ChatConfig config1 = new() { MaxOutputTokens = null };
        ChatConfig config2 = new() { MaxOutputTokens = 100 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentReasoningEffort()
    {
        // Arrange
        ChatConfig config1 = new() { ReasoningEffort = 1 };
        ChatConfig config2 = new() { ReasoningEffort = 2 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldHandleNullReasoningEffort()
    {
        // Arrange
        ChatConfig config1 = new() { ReasoningEffort = 0 };
        ChatConfig config2 = new() { ReasoningEffort = 1 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateConsistentHash_ForEmptyObject()
    {
        // Arrange
        ChatConfig config = new();

        // Act
        long hash1 = config.GenerateDBHashCode();
        long hash2 = config.GenerateDBHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }
}
