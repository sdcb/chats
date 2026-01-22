using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.UserBalances.Dtos;

public record ChargeBalanceRequest
{
    [JsonPropertyName("userId")]
    public required int UserId { get; init; }

    [JsonPropertyName("value")]
    public required decimal Amount { get; init; }
}
