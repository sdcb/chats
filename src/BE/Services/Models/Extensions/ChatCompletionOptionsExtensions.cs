using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.Extensions;

public static class ChatCompletionOptionsExtensions
{
    public static IDictionary<string, BinaryData> GetOrCreateSerializedAdditionalRawData(this ChatCompletionOptions options)
    {
        IDictionary<string, BinaryData>? rawData = GetSerializedAdditionalRawData(options);
        if (rawData == null)
        {
            rawData = new Dictionary<string, BinaryData>();
            SetSerializedAdditionalRawData(options, rawData);
        }

        return rawData;
    }

    public static void SetMaxTokens(this ChatCompletionOptions options, int value)
    {
        IDictionary<string, BinaryData>? rawData = GetSerializedAdditionalRawData(options);
        if (rawData == null)
        {
            rawData = new Dictionary<string, BinaryData>();
            SetSerializedAdditionalRawData(options, rawData);
        }
        rawData["max_tokens"] = BinaryData.FromObjectAsJson(value);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_SerializedAdditionalRawData")]
    private extern static IDictionary<string, BinaryData>? GetSerializedAdditionalRawData(ChatCompletionOptions @this);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_SerializedAdditionalRawData")]
    private extern static void SetSerializedAdditionalRawData(ChatCompletionOptions @this, IDictionary<string, BinaryData>? value);
}
