using Chats.BE.DB;
using Chats.BE.DB.Jsons;
using System.Collections.Concurrent;

namespace Chats.BE.Controllers.Chats.Chats;

public record UserModelBalanceCalculator(BalanceInitialInfo Initial, ConcurrentDictionary<string, BalanceCostInfo> Costs)
{
    private BalanceCostInfo TotalCost => BalanceCostInfo.CombineAll(Costs.Values);

    private BalanceInitialInfo Remaining => Initial - TotalCost;

    public BalanceCostInfo GetTotalCostExcept(string scopeId) => BalanceCostInfo.CombineAll(Costs.Where(x => x.Key != scopeId).Select(x => x.Value));

    private BalanceInitialInfo GetRemainingExcept(string scopeId) => Initial - GetTotalCostExcept(scopeId);


    public static UserModelBalanceCalculator Empty => new(new BalanceInitialInfo([], 0), []);

    public bool IsSufficient => Remaining.IsSufficient;

    public decimal BalanceCost => TotalCost.TotalCost;

    public IEnumerable<BalanceInitialUsageInfo> UsageCosts => TotalCost.UsageInfo.Values.Where(x => x.Counts > 0 || x.Tokens > 0);

    private BalanceCostInfo GetSpanCost(string scopeId, short modelId, int inputTokenCount, int outputTokenCount, JsonPriceConfig price)
    {
        BalanceInitialInfo remaining = GetRemainingExcept(scopeId);
        BalanceInitialUsageInfo modelUsageInfo = remaining.GetModelUsageInfo(modelId);

        // price model is based on counts
        if (modelUsageInfo.Counts > 0)
        {
            return new BalanceCostInfo(new BalanceInitialUsageInfo(modelId, Counts: 1));
        }

        // price model is based on tokens
        if (modelUsageInfo.Tokens > inputTokenCount + outputTokenCount)
        {
            //return new BalanceCostInfo(CostTokens: inputTokenCount + outputTokenCount);
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
        int toBeDeductedInputTokens = Math.Max(0, inputTokenCount - remainingTokens);

        decimal inputCost = price.InputTokenPrice * toBeDeductedInputTokens;
        decimal outputCost = price.OutputTokenPrice * toBeDeductedOutputTokens;
        return new BalanceCostInfo(new BalanceInitialUsageInfo(modelId, Tokens: modelUsageInfo.Tokens - remainingTokens), inputCost, outputCost);
    }

    public void SetSpanCost(string scopeId, short modelId, int inputTokenCount, int outputTokenCount, JsonPriceConfig price)
    {
        Costs[scopeId] = GetSpanCost(scopeId, modelId, inputTokenCount, outputTokenCount, price);
    }

    public ScopedBalanceCalculator WithScoped(string scopeId) => new(this, scopeId);
}

public class ScopedBalanceCalculator(UserModelBalanceCalculator parent, string scopeId)
{
    public bool IsSufficient => parent.IsSufficient;

    public BalanceCostInfo Cost => parent.Costs[scopeId];

    public void SetCost(short modelId, int inputTokenCount, int outputTokenCount, JsonPriceConfig price)
    {
        parent.SetSpanCost(scopeId, modelId, inputTokenCount, outputTokenCount, price);
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

public record BalanceCostInfo(Dictionary<short, BalanceInitialUsageInfo> UsageInfo, decimal InputCost = 0, decimal OutputCost = 0)
{
    public BalanceCostInfo(BalanceInitialUsageInfo usageInfo, decimal inputCost = 0, decimal outputCost = 0) : this(new Dictionary<short, BalanceInitialUsageInfo>() { [usageInfo.ModelId] = usageInfo }, inputCost, outputCost)
    {
    }

    public decimal TotalCost => InputCost + OutputCost;

    public static BalanceCostInfo CombineAll(IEnumerable<BalanceCostInfo> costs)
    {
        Dictionary<short, BalanceInitialUsageInfo> usageInfo = [];
        decimal inputTokenAmount = 0;
        decimal outputTokenAmount = 0;
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
            inputTokenAmount += cost.InputCost;
            outputTokenAmount += cost.OutputCost;
        }
        return new BalanceCostInfo(usageInfo, inputTokenAmount, outputTokenAmount);
    }
}
