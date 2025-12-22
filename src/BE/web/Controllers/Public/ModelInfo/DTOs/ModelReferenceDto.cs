using Chats.Web.DB.Enums;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Public.ModelInfo.DTOs;

public record ModelReferenceDto : SimpleModelReferenceDto
{
    [JsonPropertyName("modelProviderId")]
    public required DBModelProvider ModelProviderId { get; init; }

    [JsonPropertyName("minTemperature")]
    public required decimal MinTemperature { get; init; }

    [JsonPropertyName("maxTemperature")]
    public required decimal MaxTemperature { get; init; }

    [JsonPropertyName("allowVision")]
    public required bool AllowVision { get; init; }

    [JsonPropertyName("allowSearch")]
    public required bool AllowSearch { get; init; }

    [JsonPropertyName("contextWindow")]
    public required int ContextWindow { get; init; }

    [JsonPropertyName("maxResponseTokens")]
    public required int MaxResponseTokens { get; init; }

    [JsonPropertyName("promptTokenPrice1M")]
    public required decimal PromptTokenPrice1M { get; init; }

    [JsonPropertyName("responseTokenPrice1M")]
    public required decimal ResponseTokenPrice1M { get; init; }

    [JsonPropertyName("rawPromptTokenPrice1M")]
    public required decimal RawPromptTokenPrice1M { get; init; }

    [JsonPropertyName("rawResponseTokenPrice1M")]
    public required decimal RawResponseTokenPrice1M { get; init; }

    [JsonPropertyName("currencyCode")]
    public required string CurrencyCode { get; init; }

    [JsonPropertyName("exchangeRate")]
    public required decimal ExchangeRate { get; init; }
}
