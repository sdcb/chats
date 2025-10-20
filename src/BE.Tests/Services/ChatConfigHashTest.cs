using Chats.BE.DB;
using Chats.BE.DB.Enums;

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
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentCodeExecutionEnabled()
    {
        // Arrange
        ChatConfig config1 = new() { CodeExecutionEnabled = true };
        ChatConfig config2 = new() { CodeExecutionEnabled = false };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldMaintainCompatibility_ForDefaultCodeExecutionEnabled()
    {
        // Arrange - 测试向后兼容性：false(默认值)不应影响哈希
        ChatConfig configOld = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test",
            WebSearchEnabled = true
            // CodeExecutionEnabled 未显式设置，默认为 false
        };
        
        ChatConfig configNew = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test",
            WebSearchEnabled = true,
            CodeExecutionEnabled = false // 显式设置为 false
        };

        // Act
        long hash1 = configOld.GenerateDBHashCode();
        long hash2 = configNew.GenerateDBHashCode();

        // Assert - false 值应该产生相同的哈希以保持向后兼容
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldIncludeCodeExecutionEnabled_WhenTrue()
    {
        // Arrange - 验证当 CodeExecutionEnabled 为 true 时确实影响哈希
        ChatConfig configWithoutCodeExecution = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test",
            WebSearchEnabled = false,
            CodeExecutionEnabled = false
        };
        
        ChatConfig configWithCodeExecution = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test",
            WebSearchEnabled = false,
            CodeExecutionEnabled = true
        };

        // Act
        long hash1 = configWithoutCodeExecution.GenerateDBHashCode();
        long hash2 = configWithCodeExecution.GenerateDBHashCode();

        // Assert - true 值应该产生不同的哈希
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

    // 新增的 ImageSizeId 字段测试
    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentImageSizeId()
    {
        // Arrange
        ChatConfig config1 = new() { ImageSizeId = (short)DBKnownImageSize.Default };
        ChatConfig config2 = new() { ImageSizeId = (short)DBKnownImageSize.W1024xH1024 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldMaintainCompatibility_ForDefaultImageSizeId()
    {
        // Arrange - 测试向后兼容性：默认值(0)不应影响哈希
        ChatConfig configWithoutImageSize = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test",
            ImageSizeId = 0 // 默认值
        };
        
        ChatConfig configExplicitDefault = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test",
            ImageSizeId = (short)DBKnownImageSize.Default
        };

        // Act
        long hash1 = configWithoutImageSize.GenerateDBHashCode();
        long hash2 = configExplicitDefault.GenerateDBHashCode();

        // Assert - 默认值应该产生相同的哈希以保持向后兼容
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentImageSizes()
    {
        // Arrange
        ChatConfig config1 = new() { ImageSizeId = (short)DBKnownImageSize.W1024xH1024 };
        ChatConfig config2 = new() { ImageSizeId = (short)DBKnownImageSize.W1536xH1024 };
        ChatConfig config3 = new() { ImageSizeId = (short)DBKnownImageSize.W1024xH1536 };

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();
        long hash3 = config3.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
        Assert.NotEqual(hash2, hash3);
    }

    // 新增的 McpIds 字段测试
    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentMcpIds()
    {
        // Arrange
        ChatConfig config1 = new();
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });

        ChatConfig config2 = new();
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldMaintainCompatibility_ForEmptyMcpIds()
    {
        // Arrange - 测试向后兼容性：空的MCP关联不应影响哈希
        ChatConfig configWithoutMcps = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test"
        };
        
        ChatConfig configWithEmptyMcps = new() 
        { 
            ModelId = 1,
            SystemPrompt = "Test"
        };
        // ChatConfigMcps 默认是空集合

        // Act
        long hash1 = configWithoutMcps.GenerateDBHashCode();
        long hash2 = configWithEmptyMcps.GenerateDBHashCode();

        // Assert - 空的MCP关联应该产生相同的哈希以保持向后兼容
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateConsistentHash_ForSameMcpIdsInDifferentOrder()
    {
        // Arrange - 测试MCP ID排序的一致性
        ChatConfig config1 = new();
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 3 });
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });

        ChatConfig config2 = new();
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 3 });

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();

        // Assert - 相同的MCP ID集合应该产生相同的哈希，无论添加顺序如何
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentMcpIdCombinations()
    {
        // Arrange
        ChatConfig config1 = new();
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });

        ChatConfig config2 = new();
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 3 });

        ChatConfig config3 = new();
        config3.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();
        long hash3 = config3.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
        Assert.NotEqual(hash2, hash3);
    }

    [Fact]
    public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForCombinedNewFields()
    {
        // Arrange - 测试两个新字段的组合
        ChatConfig config1 = new() 
        { 
            ImageSizeId = (short)DBKnownImageSize.W1024xH1024
        };
        config1.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });

        ChatConfig config2 = new() 
        { 
            ImageSizeId = (short)DBKnownImageSize.W1536xH1024
        };
        config2.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });

        ChatConfig config3 = new() 
        { 
            ImageSizeId = (short)DBKnownImageSize.W1024xH1024
        };
        config3.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });

        // Act
        long hash1 = config1.GenerateDBHashCode();
        long hash2 = config2.GenerateDBHashCode();
        long hash3 = config3.GenerateDBHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2); // 不同的 ImageSizeId
        Assert.NotEqual(hash1, hash3); // 不同的 McpServerId
        Assert.NotEqual(hash2, hash3); // 两者都不同
    }
}
