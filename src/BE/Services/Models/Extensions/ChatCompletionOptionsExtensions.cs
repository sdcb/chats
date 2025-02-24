using OpenAI.Chat;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Chats.BE.Services.Models.Extensions;

public static class ChatCompletionOptionsExtensions
{
    public static bool IsSearchEnabled(this ChatCompletionOptions options)
    {
        IDictionary<string, BinaryData>? rawData = GetSerializedAdditionalRawData(options);
        if (rawData != null && rawData.TryGetValue("enable_search", out BinaryData? binaryData))
        {
            return binaryData.ToObjectFromJson<bool>();
        }
        return false;
    }

    public static void SetWebSearchEnabled_QwenStyle(this ChatCompletionOptions options, bool value)
    {
        IDictionary<string, BinaryData>? rawData = GetSerializedAdditionalRawData(options);
        if (rawData == null)
        {
            rawData = new Dictionary<string, BinaryData>();
            SetSerializedAdditionalRawData(options, rawData);
        }

        rawData["enable_search"] = BinaryData.FromObjectAsJson(value);
    }

    public static void SetWebSearchEnabled_QianFanStyle(this ChatCompletionOptions options, bool value)
    {
        IDictionary<string, BinaryData>? rawData = GetSerializedAdditionalRawData(options);
        if (rawData == null)
        {
            rawData = new Dictionary<string, BinaryData>();
            SetSerializedAdditionalRawData(options, rawData);
        }

        rawData["web_search"] = BinaryData.FromObjectAsJson(new Dictionary<string, object>()
        {
            ["enable"] = true,
            ["enable_citation"] = false,
            ["enable_trace"] = false,
        });
    }

    public static ulong? GetDashScopeSeed(this ChatCompletionOptions options)
    {
        IDictionary<string, BinaryData>? rawData = GetSerializedAdditionalRawData(options);
        if (rawData != null && rawData.TryGetValue("seed", out BinaryData? binaryData))
        {
            return binaryData.ToObjectFromJson<ulong>();
        }
        return null;
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
