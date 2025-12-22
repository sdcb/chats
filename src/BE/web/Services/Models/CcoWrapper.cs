using Chats.Web.DB.Enums;
using Chats.Web.Services.Models.ChatServices.OpenAI;
using Chats.Web.Services.Models.Neutral;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models;

public class CcoWrapper(JsonObject json)
{
    public bool Streamed
    {
        get => (bool?)json["stream"] ?? false;
        set => SetOrRemove("stream", value);
    }

    public string Model
    {
        get => (string)json["model"]!;
        set => SetOrRemove("model", value);
    }

    public JsonArray? MessagesJson => json["messages"]?.AsArray();

    public IList<NeutralMessage> Messages
    {
        get => NeutralConversions.ParseOpenAIMessages(MessagesJson);
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
        return Model != null && MessagesJson != null;
    }

    public string? SystemPrompt => NeutralConversions.ExtractSystemPrompt(MessagesJson);

    public float? Temperature => (float?)json["temperature"];

    public int? MaxOutputTokens => (int?)json["max_tokens"] ?? (int?)json["max_completion_tokens"];

    public string? ReasoningEffort => (string?)json["reasoning_effort"];

    public bool? AllowParallelToolCalls => (bool?)json["parallel_tool_calls"];

    public ChatResponseFormat? ResponseFormat
    {
        get
        {
            JsonNode? rf = json["response_format"];
            if (rf == null) return null;

            string? type = (string?)rf["type"];
            if (type == null || type == "text") return ChatResponseFormat.Text;
            if (type == "json_object") return ChatResponseFormat.JsonObject;
            if (type == "json_schema")
            {
                JsonNode? schemaNode = rf["json_schema"];
                if (schemaNode == null) return null;
                string? name = (string?)schemaNode["name"];
                JsonNode? schema = schemaNode["schema"];
                bool? strict = (bool?)schemaNode["strict"];
                if (name == null || schema == null) return null;
                return ChatResponseFormat.CreateJsonSchema(name, schema.AsObject(), strict);
            }
            return null;
        }
    }

    public IList<ChatTool> Tools
    {
        get
        {
            List<ChatTool> tools = [];
            JsonArray? toolsArray = json["tools"]?.AsArray();
            if (toolsArray == null) return tools;

            foreach (JsonNode? toolNode in toolsArray)
            {
                if (toolNode == null) continue;
                string? type = (string?)toolNode["type"];
                if (type != "function") continue;

                JsonNode? function = toolNode["function"];
                if (function == null) continue;

                string? name = (string?)function["name"];
                if (string.IsNullOrEmpty(name)) continue;

                string? description = (string?)function["description"];
                JsonNode? parameters = function["parameters"];

                tools.Add(FunctionTool.Create(
                    name,
                    description,
                    parameters?.ToJsonString()
                ));
            }
            return tools;
        }
    }

    public float? TopP => (float?)json["top_p"];

    public long? Seed => (long?)json["seed"];

    public bool? EnableSearch => (bool?)json["enable_search"];

    public string? ImageSize => (string?)json["image_size"];

    public bool? EnableCodeExecution => (bool?)json["enable_code_execution"];

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

    public string Serialize()
    {
        return json.ToJsonString(JSON.JsonSerializerOptions);
    }

    /// <summary>
    /// Returns this wrapper - previously used for SDK-specific conversion but now just returns itself
    /// </summary>
    public CcoWrapper ToCleanCco() => this;
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
