using Chats.Web.DB.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.Web.DB.Jsons;

public record JsonTokenBalance
{
    [JsonPropertyName("modelId")]
    public required short ModelId { get; init; }

    [JsonPropertyName("tokens"), Range(0, int.MaxValue / 2)]
    public required int Tokens { get; init; }

    [JsonPropertyName("counts"), Range(0, int.MaxValue / 2)]
    public required int Counts { get; init; }

    [JsonPropertyName("expires")]
    public required DateTime Expires { get; init; }

    public bool ApplyTo(UserModel existingItem, int? creditUserId, out UsageTransaction? usageTransaction)
    {
        bool needsTransaction = existingItem.CountBalance != Counts || existingItem.TokenBalance != Tokens;
        bool hasDifference = needsTransaction || existingItem.ExpiresAt != Expires;

        if (needsTransaction)
        {
            usageTransaction = new UsageTransaction()
            {
                CreatedAt = DateTime.UtcNow,
                CountAmount = Counts - existingItem.CountBalance,
                TokenAmount = Tokens - existingItem.TokenBalance,
                ModelId = ModelId,
                TransactionTypeId = (byte)DBTransactionType.Charge,
            };
            ApplyCreditUser(existingItem, creditUserId, usageTransaction);
        }
        else
        {
            usageTransaction = null;
        }

        if (hasDifference)
        {
            existingItem.CountBalance = Counts;
            existingItem.TokenBalance = Tokens;
            existingItem.ExpiresAt = Expires;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }

        return hasDifference;
    }

    private static void ApplyCreditUser(UserModel existingItem, int? creditUserId, UsageTransaction ut)
    {
        if (creditUserId.HasValue)
        {
            ut.CreditUserId = creditUserId.Value;
        }
        else if (existingItem.User != null)
        {
            ut.CreditUser = existingItem.User;
        }
        else
        {
            ut.CreditUserId = existingItem.UserId;
        }
    }
}
