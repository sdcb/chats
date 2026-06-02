using Chats.BE.Services.Common;

namespace Chats.BE.UnitTest.Common;

public class MaskedKeyUtilsTest
{
    public static IEnumerable<object?[]> MaskCases()
    {
        yield return [null, null];
        yield return [string.Empty, string.Empty];
        yield return ["This is not a JSON string.", "This ****g."];
        yield return ["[\"value1\",\"value2\"]", "[\"val****\"]"];
        yield return ["simpletext", "simpl****xt"];
        yield return ["{\"key1\":\"value12345\",\"key2\":\"short\",\"key3\":\"value67890\"}", Normalize("""
            {
              "key1": "value****45",
              "key2": "short",
              "key3": "value****90"
            }
            """)];
        yield return ["{\"key1\":\"short\",\"key2\":\"1234567\"}", Normalize("""
            {
              "key1": "short",
              "key2": "1234567"
            }
            """)];
        yield return ["{\"key1\":\"longvalue123\",\"key2\":\"anotherlongvalue\"}", Normalize("""
            {
              "key1": "longv****23",
              "key2": "anoth****ue"
            }
            """)];
        yield return ["{\"key1\":null,\"key2\":\"value12345\"}", Normalize("""
            {
              "key1": null,
              "key2": "value****45"
            }
            """)];
    }

    [Theory]
    [MemberData(nameof(MaskCases))]
    public void JsonToMaskedNull_ShouldMaskSupportedInputs(string? input, string? expected)
    {
        string? actual = MaskedKeyUtils.JsonToMaskedNull(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonToMaskedNull_NestedObject_ThrowsInvalidOperationException()
    {
        const string input = "{\"key1\":\"value1\",\"key2\":{\"nestedKey\":\"nestedValue\"}}";

        Assert.Throws<InvalidOperationException>(() => MaskedKeyUtils.JsonToMaskedNull(input));
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n");
    }
}
