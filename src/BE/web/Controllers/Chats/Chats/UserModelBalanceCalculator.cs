using Chats.DB;
using Chats.BE.DB.Jsons;

namespace Chats.BE.Controllers.Chats.Chats;

public class BalanceCalculator(BalanceInitialInfo initial)
{
    private BalanceCostInfo _cost = new([], 0, 0, 0);

    private BalanceInitialInfo Remaining => initial - _cost;

    public bool IsSufficient => Remaining.IsSufficient;

    public BalanceCostInfo Cost => _cost;

    public decimal BalanceCost => _cost.TotalCost;

    public IEnumerable<BalanceInitialUsageInfo> UsageCosts => _cost.UsageInfo.Values.Where(x => x.Counts > 0 || x.Tokens > 0);

    public void SetCost(short modelId, int inputTokenCount, int outputTokenCount, int cacheTokenCount, JsonPriceConfig price)
    {
        _cost = GetCost(modelId, inputTokenCount, outputTokenCount, cacheTokenCount, price);
    }

    private BalanceCostInfo GetCost(short modelId, int inputTokenCount, int outputTokenCount, int cacheTokenCount, JsonPriceConfig price)
    {
        BalanceInitialUsageInfo modelUsageInfo = initial.GetModelUsageInfo(modelId);

        // price model is based on counts
        if (modelUsageInfo.Counts > 0)
        {
            return new BalanceCostInfo(new BalanceInitialUsageInfo(modelId, Counts: 1));
        }

        // price model is based on tokens
        if (modelUsageInfo.Tokens > inputTokenCount + outputTokenCount)
        {
            return new BalanceCostInfo(new BalanceInitialUsageInfo(modelId, Tokens: inputTokenCount + outputTokenCount));
        }

        // token count not enough, check balance by remaining toBeDeductedInputTokens/toBeDeductedOutputTokens
        // calculate toBeDeductedOutputTokens first because it's typically more expensive

        // for example, if inputTokenCount = 100, outputTokenCount = 200, Tokens = 250, then:
        // toBeDeductedOutputTokens = 200-250 = -50(0), and then remaining tokens is 50
        // toBeDeductedInputTokens = 100-50 = 50

        // another example, if inputTokenCount = 100, outputTokenCount = 200, Tokens = 50, then:
        // toBeDeductedOutputTokens = 200-50 = 150, and then remaining tokens is 0
        // toBeDeductedInputTokens = 100-0 = 100
        int remainingTokens = modelUsageInfo.Tokens;
        int toBeDeductedOutputTokens = Math.Max(0, outputTokenCount - remainingTokens);
        remainingTokens = Math.Max(0, remainingTokens - outputTokenCount);

        cacheTokenCount = Math.Clamp(cacheTokenCount, 0, inputTokenCount);
        int normalInputTokens = inputTokenCount - cacheTokenCount;

        int normalTokensCharged = Math.Max(0, normalInputTokens - remainingTokens);
        int allowanceAfterNormal = Math.Max(0, remainingTokens - normalInputTokens);
        int cacheTokensCharged = Math.Max(0, cacheTokenCount - allowanceAfterNormal);

        decimal inputCost = price.InputFreshTokenPrice * normalTokensCharged;
        decimal cacheCost = price.InputCachedTokenPrice * cacheTokensCharged;
        decimal outputCost = price.OutputTokenPrice * toBeDeductedOutputTokens;
        return new BalanceCostInfo(new BalanceInitialUsageInfo(modelId, Tokens: modelUsageInfo.Tokens - remainingTokens), inputCost, outputCost, cacheCost);
    }
}

public record BalanceInitialInfo(Dictionary<short, BalanceInitialUsageInfo> UsageInfo, decimal Balance)
{
    public bool IsSufficient => UsageInfo.Values.All(x => x.IsSufficient) && Balance >= 0;

    public BalanceInitialUsageInfo GetModelUsageInfo(short modelId)
    {
        if (UsageInfo.TryGetValue(modelId, out BalanceInitialUsageInfo? value))
        {
            return value;
        }
        return new BalanceInitialUsageInfo(modelId, 0, 0);
    }

    public static BalanceInitialInfo FromDB(IEnumerable<UserModel> userModels, decimal balance)
    {
        // there won't be any duplicate modelId in userModels
        return new BalanceInitialInfo(
            userModels.ToDictionary(x => x.ModelId, x => new BalanceInitialUsageInfo(x.ModelId, x.CountBalance, x.TokenBalance)),
            balance);
    }

    public static BalanceInitialInfo operator-(BalanceInitialInfo initial, BalanceCostInfo cost)
    {
        Dictionary<short, BalanceInitialUsageInfo> usageInfo = new(initial.UsageInfo);
        foreach (KeyValuePair<short, BalanceInitialUsageInfo> item in cost.UsageInfo)
        {
            if (usageInfo.TryGetValue(item.Key, out BalanceInitialUsageInfo? value))
            {
                usageInfo[item.Key] = new BalanceInitialUsageInfo(item.Key, value.Counts - item.Value.Counts, value.Tokens - item.Value.Tokens);
            }
            else
            {
                usageInfo[item.Key] = item.Value;
            }
        }
        return new BalanceInitialInfo(usageInfo, initial.Balance - cost.TotalCost);
    }
}

public record BalanceInitialUsageInfo(short ModelId, int Counts = 0, int Tokens = 0)
{
    public bool IsSufficient => Counts >= 0 && Tokens >= 0;
}

public record BalanceCostInfo(Dictionary<short, BalanceInitialUsageInfo> UsageInfo, decimal InputFreshCost = 0, decimal OutputCost = 0, decimal InputCachedCost = 0)
{
    public BalanceCostInfo(BalanceInitialUsageInfo usageInfo, decimal inputFreshCost = 0, decimal outputCost = 0, decimal inputCachedCost = 0)
        : this(new Dictionary<short, BalanceInitialUsageInfo>() { [usageInfo.ModelId] = usageInfo }, inputFreshCost, outputCost, inputCachedCost)
    {
    }

    public decimal TotalCost => InputFreshCost + OutputCost + InputCachedCost;

    public static BalanceCostInfo CombineAll(IEnumerable<BalanceCostInfo> costs)
    {
        Dictionary<short, BalanceInitialUsageInfo> usageInfo = [];
        decimal inputFreshTokenAmount = 0;
        decimal outputTokenAmount = 0;
        decimal inputCachedTokenAmount = 0;
        foreach (BalanceCostInfo cost in costs)
        {
            foreach (KeyValuePair<short, BalanceInitialUsageInfo> item in cost.UsageInfo)
            {
                if (usageInfo.TryGetValue(item.Key, out BalanceInitialUsageInfo? value))
                {
                    usageInfo[item.Key] = new BalanceInitialUsageInfo(item.Key, value.Counts + item.Value.Counts, value.Tokens + item.Value.Tokens);
                }
                else
                {
                    usageInfo[item.Key] = item.Value;
                }
            }
            inputFreshTokenAmount += cost.InputFreshCost;
            outputTokenAmount += cost.OutputCost;
            inputCachedTokenAmount += cost.InputCachedCost;
        }
        return new BalanceCostInfo(usageInfo, inputFreshTokenAmount, outputTokenAmount, inputCachedTokenAmount);
    }
}
