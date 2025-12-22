using System.Text.Json.Serialization;

namespace Chats.Web.DB.Jsons;

public record JsonFileConfig
{
    [JsonPropertyName("count"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; init; }

    [JsonPropertyName("maxSize"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxSize { get; init; }

    public static JsonFileConfig Default => new()
    {
        Count = 5,
        MaxSize = 10240
    };
}
