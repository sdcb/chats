using Chats.Web.DB;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.Models.Dtos;

public record ModelUsageDto
{
    [JsonPropertyName("counts")]
    public required int Counts { get; init; }

    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }

    [JsonPropertyName("isTerm")]
    public required bool IsTerm { get; init; }

    [JsonPropertyName("inputFreshTokenPrice1M")]
    public required decimal InputFreshTokenPrice1M { get; init; }

    [JsonPropertyName("outputTokenPrice1M")]
    public required decimal OutputTokenPrice1M { get; init; }

    [JsonPropertyName("inputCachedTokenPrice1M")]
    public required decimal InputCachedTokenPrice1M { get; init; }

    public static ModelUsageDto FromDB(UserModel userModel)
    {
        return new ModelUsageDto
        {
            Counts = userModel.CountBalance,
            Expires = userModel.ExpiresAt,
            IsTerm = userModel.ExpiresAt - DateTime.UtcNow > TimeSpan.FromDays(365 * 2),
            InputFreshTokenPrice1M = userModel.Model.InputFreshTokenPrice1M,
            OutputTokenPrice1M = userModel.Model.OutputTokenPrice1M,
            InputCachedTokenPrice1M = userModel.Model.InputCachedTokenPrice1M,
            Tokens = userModel.TokenBalance,
        };
    }
}
