using Chats.BE.Controllers.Api.OpenAICompatible.Dtos;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Chats.BE.Services;

public static class JSON
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 用于 API 响应的序列化选项，排除 OpenAIFullResponse.Segments 字段
    /// </summary>
    public static JsonSerializerOptions ApiJsonSerializerOptions { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { ExcludeSegmentsModifier }
        }
    };

    private static void ExcludeSegmentsModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(OpenAIFullResponse))
        {
            foreach (JsonPropertyInfo prop in typeInfo.Properties)
            {
                if (prop.Name == "segments")
                {
                    prop.ShouldSerialize = (_, _) => false;
                }
            }
        }
    }

    public static string Serialize(object? obj)
    {
        return JsonSerializer.Serialize(obj, JsonSerializerOptions);
    }

    public static byte[] SerializeToUtf8Bytes(object obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj, JsonSerializerOptions);
    }
}
