using System.Text.Encodings.Web;
using System.Text.Json;

namespace Chats.BE.Services;

public static class JSON
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(object? obj)
    {
        return JsonSerializer.Serialize(obj, JsonSerializerOptions);
    }

    public static byte[] SerializeToUtf8Bytes(object obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj, JsonSerializerOptions);
    }
}
