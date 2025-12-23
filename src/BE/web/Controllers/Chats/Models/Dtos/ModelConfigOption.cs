using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Models.Dtos;

public record ModelConfigOption
{
    [JsonPropertyName("temperature")]
    public required TemperatureOptions Temperature { get; init; }

    [JsonPropertyName("enableSearch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableSearch { get; init; }

    public static ModelConfigOption FromTemperature(TemperatureOptions temperatureOptions)
    {
        return new ModelConfigOption
        {
            Temperature = temperatureOptions
        };
    }
}
