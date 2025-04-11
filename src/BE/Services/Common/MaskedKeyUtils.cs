using System.Text.Encodings.Web;
using System.Text.Json;

namespace Chats.BE.Services.Common;

internal static class MaskedKeyUtils
{
    public static string ToMasked(this string key)
    {
        return key.Length > 7 ? key[..5] + "****" + key[^2..] : key;
    }

    public static string? ToMaskedNull(this string? key)
    {
        return key is not null && key.Length > 7 ? key[..5] + "****" + key[^2..] : key;
    }

    private static JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        IndentCharacter = ' ',
        NewLine = "\n",
        IndentSize = 2,
    };

    public static string? JsonToMaskedNull(this string? jsonKey)
    {
        if (jsonKey is null)
        {
            return null;
        }

        if (!IsProbablyValidJson(jsonKey))
        {
            return ToMaskedNull(jsonKey);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonKey);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                // 根元素不是对象，直接对整个字符串进行处理
                return ToMaskedNull(jsonKey);
            }

            // 创建一个字典来存储修改后的键值对
            Dictionary<string, object?> modifiedDict = [];

            foreach (JsonProperty property in root.EnumerateObject())
            {
                JsonElement value = property.Value;

                // 检查是否存在嵌套对象或数组
                if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
                {
                    // 发现嵌套对象或数组，抛出异常
                    throw new InvalidOperationException("Nested objects are not supported.");
                }

                // 获取值的字符串表示
                string? valueString = value.GetString();

                // 对值进行处理
                var maskedValue = ToMaskedNull(valueString);

                modifiedDict[property.Name] = maskedValue;
            }

            // 序列化修改后的字典为 JSON 字符串
            string result = JsonSerializer.Serialize(modifiedDict, _jsonOptions);
            return result;
        }
        catch (JsonException)
        {
            // 解析失败，可能是无效的 JSON，直接对整个字符串进行处理
            return ToMaskedNull(jsonKey);
        }
    }

    private static bool IsProbablyValidJson(string json)
    {
        json = json.Trim();
        return (json.StartsWith('{') && json.EndsWith('}')) ||
               (json.StartsWith('[') && json.EndsWith(']'));
    }

    public static bool SeemsMasked(this string? key)
    {
        return key is not null && key.Contains("****");
    }

    public static bool IsMaskedEquals(this string? key, string? inputKey)
    {
        return inputKey.SeemsMasked() 
            ? JsonToMaskedNull(key) == inputKey 
            : key == inputKey;
    }
}
