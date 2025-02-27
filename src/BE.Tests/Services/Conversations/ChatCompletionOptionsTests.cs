using Chats.BE.Services.Models.Extensions;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace Chats.BE.Tests.Services.Conversations;

public class ChatCompletionOptionsTests
{
    private static ChatCompletionOptions CreateCCOWithDictionary(Dictionary<string, BinaryData> data)
    {
        ChatCompletionOptions options = new();
        SetSerializedAdditionalRawData(options, data);
        return options;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_SerializedAdditionalRawData")]
    extern static void SetSerializedAdditionalRawData(ChatCompletionOptions @this, IDictionary<string, BinaryData> data);

    [Fact]
    public void IsSearchEnabled_ReturnsTrue_WhenEnableSearchIsTrue()
    {
        // Arrange
        var options = CreateCCOWithDictionary(new Dictionary<string, BinaryData>
        {
            { "enable_search", BinaryData.FromObjectAsJson(true) }
        });

        // Act
        bool result = IsSearchEnabled(options);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSearchEnabled_ReturnsFalse_WhenEnableSearchIsFalse()
    {
        // Arrange
        var options = CreateCCOWithDictionary(new Dictionary<string, BinaryData>
        {
            { "enable_search", BinaryData.FromObjectAsJson(false) }
        });

        // Act
        bool result = IsSearchEnabled(options);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSearchEnabled_ReturnsFalse_WhenEnableSearchIsNotPresent()
    {
        // Arrange
        var options = CreateCCOWithDictionary([]);

        // Act
        bool result = IsSearchEnabled(options);

        // Assert
        Assert.False(result);
    }

    static bool IsSearchEnabled(ChatCompletionOptions options)
    {
        IDictionary<string, BinaryData>? rawData = options.GetOrCreateSerializedAdditionalRawData();
        if (rawData != null && rawData.TryGetValue("enable_search", out BinaryData? binaryData))
        {
            return binaryData.ToObjectFromJson<bool>();
        }
        return false;
    }
}
