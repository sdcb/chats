using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Models.Dtos;

public record TemperatureOptions(
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("max")] decimal Max
)
{
    public float Clamp(float value)
    {
        return Math.Clamp(value, (float)Min, (float)Max);
    }
}