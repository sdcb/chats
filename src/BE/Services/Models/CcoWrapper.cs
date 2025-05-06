using Json.More;
using OpenAI.Chat;
using System.Buffers.Binary;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models;

public class CcoWrapper(JsonObject json)
{
    public bool Stream
    {
        get => (bool?)json["stream"] ?? false;
        set => SetOrRemove("stream", value);
    }

    public string Model
    {
        get => (string)json["model"]!;
        set => SetOrRemove("model", value);
    }

    public IList<ChatMessage>? Messages
    {
        get => json["messages"]
            ?.AsArray()
            .Select(x => ModelReaderWriter.Read<ChatMessage>(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(x)))!)
            .ToArray();
        set => SetOrRemove("messages", value != null
            ? new JsonArray(value.Select(x => JsonNode.Parse(ModelReaderWriter.Write(x).ToArray())).ToArray())
            : null);
    }

    public CcoCacheControl? CacheControl
    {
        get => CcoCacheControl.Parse(json["cache"]);
        set
        {
            if (value is null)
            {
                json.Remove("cache");
            }
            else
            {
                SetOrRemove("cache", value.ToJSON());
            }
        }
    }

    public bool SeemsValid()
    {
        return Model != null && Messages != null;
    }

    public ChatCompletionOptions ToCleanCco()
    {
        JsonObject newOne = (JsonObject)json.DeepClone();
        newOne.Remove("stream");
        newOne.Remove("model");
        newOne.Remove("messages");
        return ModelReaderWriter.Read<ChatCompletionOptions>(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(newOne)))!;
    }

    private void SetOrRemove(string key, JsonNode? value)
    {
        if (value is null)
        {
            json.Remove(key);
        }
        else
        {
            json[key] = value;
        }
    }
}

public record CcoCacheControl
{
    public required bool CreateOnly { get; init; }
    public required int Ttl { get; init; }   // 0 表示用默认 TTL

    public DateTime ExpiresAt
    {
        get
        {
            if (Ttl == 0) return DateTime.UtcNow.AddDays(30); // 默认 TTL: 30 天
            return DateTime.UtcNow.AddSeconds(Ttl);
        }
    }

    public static CcoCacheControl StaticCached { get; } = new CcoCacheControl
    {
        CreateOnly = false,
        Ttl = 0
    };

    public static CcoCacheControl? Parse(JsonNode? node)
    {
        // 1) null (或 JSON null) → 无配置
        if (node is null || node.GetValue<object?>() is null)
            return null;

        // 2) boolean
        if (node is JsonValue jvBool && jvBool.TryGetValue<bool>(out bool b))
        {
            return b
                 ? new CcoCacheControl { CreateOnly = false, Ttl = 0 }   // true
                 : null;                                                // false = 不使用缓存
        }

        // 3) string
        if (node is JsonValue jvStr && jvStr.TryGetValue<string>(out string? raw))
        {
            raw = raw.Trim();

            // "createOnly"
            if (raw.Equals("createOnly", StringComparison.OrdinalIgnoreCase))
            {
                return new CcoCacheControl { CreateOnly = true, Ttl = 0 };
            }

            // "createOnly:3600"
            const string prefix = "createOnly:";
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string ttlPart = raw[prefix.Length..];

                if (!int.TryParse(ttlPart, NumberStyles.None, CultureInfo.InvariantCulture, out int ttl) || ttl <= 0)
                {
                    throw new FormatException($"Invalid TTL value in '{raw}'.");
                }

                return new CcoCacheControl { CreateOnly = true, Ttl = ttl };
            }

            throw new FormatException($"Unrecognized cache control string '{raw}'.");
        }

        // 4) 其它 JSON 形态一律视为非法
        throw new FormatException("Cache control must be null, boolean, or string.");
    }

    public JsonNode ToJSON()
    {
        if (CreateOnly)
        {
            return Ttl == 0 ? "createOnly" : $"createOnly:{Ttl}";
        }
        else
        {
            return true;
        }
    }
}