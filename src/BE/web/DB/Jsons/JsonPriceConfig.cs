using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Chats.Web.DB.Jsons;

public record JsonPriceConfig
{
    [JsonPropertyName("input_fresh")]
    public required decimal InputFreshTokenPrice { get; init; }

    [JsonPropertyName("out")]
    public required decimal OutputTokenPrice { get; init; }

    [JsonPropertyName("input_cached")]
    public required decimal InputCachedTokenPrice { get; init; }

    public JsonPriceConfig1M To1M()
    {
        return new JsonPriceConfig1M
        {
            InputFreshTokenPrice1M = InputFreshTokenPrice * JsonPriceConfig1M.Unit,
            OutputTokenPrice1M = OutputTokenPrice * JsonPriceConfig1M.Unit,
            InputCachedTokenPrice1M = InputCachedTokenPrice * JsonPriceConfig1M.Unit
        };
    }

    public bool IsFree()
    {
        return InputFreshTokenPrice == 0 && OutputTokenPrice == 0 && InputCachedTokenPrice == 0;
    }
}

public record JsonPriceConfig1M
{
    [JsonPropertyName("input_fresh")]
    public required decimal InputFreshTokenPrice1M { get; init; }

    [JsonPropertyName("out")]
    public required decimal OutputTokenPrice1M { get; init; }

    [JsonPropertyName("input_cached")]
    public required decimal InputCachedTokenPrice1M { get; init; }

    public static decimal Unit = 1_000_000;

    [SetsRequiredMembers]
    public JsonPriceConfig1M(decimal inputFreshTokenPrice1M, decimal outputTokenPrice1M, decimal inputCachedTokenPrice1M)
    {
        InputFreshTokenPrice1M = inputFreshTokenPrice1M;
        OutputTokenPrice1M = outputTokenPrice1M;
        InputCachedTokenPrice1M = inputCachedTokenPrice1M;
    }

    public JsonPriceConfig1M() { }

    public JsonPriceConfig ToRaw()
    {
        return new JsonPriceConfig
        {
            InputFreshTokenPrice = InputFreshTokenPrice1M / Unit,
            OutputTokenPrice = OutputTokenPrice1M / Unit,
            InputCachedTokenPrice = InputCachedTokenPrice1M / Unit,
        };
    }

    public override string ToString()
    {
        return $"{InputFreshTokenPrice1M:F2}/{OutputTokenPrice1M:F2}/{InputCachedTokenPrice1M:F2}";
    }
}
