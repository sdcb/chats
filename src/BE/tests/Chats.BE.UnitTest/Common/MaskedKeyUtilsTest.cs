using Chats.BE.Services.Common;

namespace Chats.BE.UnitTest.Common;

public class MaskedKeyUtilsTest
{
    [Fact]
    public void JsonToMaskedNull_NullInput_ReturnsNull()
    {
        string? input = null;
        string? expected = null;

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_EmptyString_ReturnsEmptyString()
    {
        string? input = string.Empty;
        string? expected = string.Empty;

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_ValidJsonWithSimpleValues_MasksValues()
    {
        string? input = "{\"key1\":\"value12345\",\"key2\":\"short\",\"key3\":\"value67890\"}";
        string? expected = """
            {
              "key1": "value****45",
              "key2": "short",
              "key3": "value****90"
            }
            """.Replace("\r\n", "\n");

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_ValuesShorterThan7Chars_NoMasking()
    {
        string? input = "{\"key1\":\"short\",\"key2\":\"1234567\"}";
        string? expected = """
            {
              "key1": "short",
              "key2": "1234567"
            }
            """.Replace("\r\n", "\n");

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_ValuesLongerThan7Chars_Masked()
    {
        string? input = "{\"key1\":\"longvalue123\",\"key2\":\"anotherlongvalue\"}";
        string? expected = """
            {
              "key1": "longv****23",
              "key2": "anoth****ue"
            }
            """.Replace("\r\n", "\n");

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_NestedObject_ThrowsInvalidOperationException()
    {
        string? input = "{\"key1\":\"value1\",\"key2\":{\"nestedKey\":\"nestedValue\"}}";

        Assert.Throws<InvalidOperationException>(() => MaskedKeyUtils.JsonToMaskedNull(input));
    }

    [Fact]
    public void JsonToMaskedNull_InvalidJsonString_ProcessesAsRegularString()
    {
        string? input = "This is not a JSON string.";
        string? expected = "This ****g.";

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_JsonArray_Root_NotObject_ProcessesAsRegularString()
    {
        string? input = "[\"value1\",\"value2\"]";
        string? expected = "[\"val****\"]";

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_NonJsonString_ProcessesAsRegularString()
    {
        string? input = "simpletext";
        string? expected = "simpl****xt";

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_ValuesWithNull_DoesNotThrowException()
    {
        string? input = "{\"key1\":null,\"key2\":\"value12345\"}";
        string? expected = """
            {
              "key1": null,
              "key2": "value****45"
            }
            """.Replace("\r\n", "\n");

        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }
}
