using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.Statistics.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Chats.BE.Controllers.Admin.Statistics;

[Route("api/admin/statistics"), AuthorizeAdmin]
public class StatisticsController(ChatsDB db) : ControllerBase
{
    [HttpGet("enabled-user-count")]
    public async Task<int> GetEnabledUserCount(CancellationToken cancellationToken)
    {
        return await db.Users
            .Where(x => x.Enabled)
            .CountAsync(cancellationToken);
    }

    [HttpGet("enabled-model-count")]
    public async Task<int> GetEnabledModelCount(CancellationToken cancellationToken)
    {
        return await db.Models
            .Where(x => x.IsDeleted)
            .CountAsync(cancellationToken);
    }

    [HttpGet("tokens-during")]
    public async Task<long> GetTotalTokens([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        return await q.SumAsync(x => x.InputTokens + x.OutputTokens, cancellationToken);
    }

    [HttpGet("cost-during")]
    public async Task<decimal> GetTotalCost([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        return await q.SumAsync(x => x.InputCost + x.OutputCost, cancellationToken);
    }

    [HttpGet("model-provider-statistics")]
    public async Task<ActionResult<KeyValuePair<string, int>[]>> GetModelProviderStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        Dictionary<string, int> r = await q
            .GroupBy(x => x.Model.ModelKey.ModelProviderId)
            .Select(g => new { ProviderId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ContinueWith(t => t.Result.ToDictionary(
                x => ModelProviderInfo.GetName((DBModelProvider)x.ProviderId),
                x => x.Count));
        return Ok(r);
    }

    [HttpGet("model-statistics")]
    public async Task<ActionResult<SingleValueStatisticsEntry[]>> GetModelStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        SingleValueStatisticsEntry[] r = await q
            .GroupBy(x => x.Model.DeploymentName)
            .Select(x => new SingleValueStatisticsEntry(x.Key, x.Count()))
            .ToArrayAsync(cancellationToken);
        return Ok(r);
    }

    [HttpGet("model-key-statistics")]
    public async Task<ActionResult<SingleValueStatisticsEntry[]>> GetModelKeyStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        SingleValueStatisticsEntry[] r = await q
            .GroupBy(x => x.Model.ModelKey.Name)
            .Select(x => new SingleValueStatisticsEntry(x.Key, x.Count()))
            .ToArrayAsync(cancellationToken);
        return Ok(r);
    }

    [HttpGet("source-statistics")]
    public async Task<ActionResult<SingleValueStatisticsEntry[]>> GetSourceStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        SingleValueStatisticsEntry[] r = await q
            .GroupBy(x => x.UserApiUsage != null ? UsageSource.Api : UsageSource.WebChat)
            .Select(x => new SingleValueStatisticsEntry(x.Key.ToString(), x.Count()))
            .ToArrayAsync(cancellationToken);
        return Ok(r);
    }

    [HttpGet("token-statistics-by-date")]
    public async Task<ActionResult<List<DateStatisticsEntry<TokenStatisticsEntry>>>> GetTokenStatisticsByDate([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IOrderedQueryable<IGrouping<DateOnly, UserModelUsage>> q = GetUserModelQueryGrouped(query);
        DateStatisticsEntry<TokenStatisticsEntry>[] r = await q
            .Select(x => new DateStatisticsEntry<TokenStatisticsEntry>(x.Key, new TokenStatisticsEntry
            {
                InputTokens = x.Sum(y => y.InputTokens),
                OutputTokens = x.Sum(y => y.OutputTokens),
                ReasoningTokens = x.Sum(y => y.ReasoningTokens)
            }))
            .ToArrayAsync(cancellationToken);
        return Ok(FillMissing(r));
    }

    [HttpGet("cost-statistics-by-date")]
    public async Task<ActionResult<List<DateStatisticsEntry<CostStatisticsEntry>>>> GetCostStatisticsByDate([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IOrderedQueryable<IGrouping<DateOnly, UserModelUsage>> q = GetUserModelQueryGrouped(query);
        DateStatisticsEntry<CostStatisticsEntry>[] r = await q
            .Select(x => new DateStatisticsEntry<CostStatisticsEntry>(x.Key, new CostStatisticsEntry
            {
                InputCost = x.Sum(y => y.InputCost),
                OutputCost = x.Sum(y => y.OutputCost),
            }))
            .ToArrayAsync(cancellationToken);
        return Ok(FillMissing(r));
    }

    [HttpGet("chat-count-by-date")]
    public async Task<ActionResult<List<DateStatisticsEntry<int>>>> GetChatCountByDate([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IOrderedQueryable<IGrouping<DateOnly, UserModelUsage>> q = GetUserModelQueryGrouped(query);
        DateStatisticsEntry<int>[] r = await q
            .Select(x => new DateStatisticsEntry<int>(x.Key, x.Count()))
            .ToArrayAsync(cancellationToken);
        return Ok(FillMissing(r));
    }

    static List<DateStatisticsEntry<T>> FillMissing<T>(DateStatisticsEntry<T>[] dict) where T : new()
    {
        List<DateStatisticsEntry<T>> result = [];

        if (dict == null || dict.Length == 0)
        {
            return result;
        }

        result.Add(dict[0]);

        for (int i = 1; i < dict.Length; i++)
        {
            DateStatisticsEntry<T> prev = dict[i - 1];
            DateStatisticsEntry<T> current = dict[i];

            DateOnly expectedNextDate = prev.Date.AddDays(1);

            while (expectedNextDate < current.Date)
            {
                result.Add(new DateStatisticsEntry<T>(expectedNextDate, new T()));
                expectedNextDate = expectedNextDate.AddDays(1);
            }

            result.Add(current);
        }

        return result;
    }

    private IQueryable<UserModelUsage> GetUserModelQuery(StartEndDate query)
    {
        IQueryable<UserModelUsage> q = db.UserModelUsages;
        if (query.StartDate != null)
        {
            q = q.Where(x => x.CreatedAt >= query.StartDate);
        }
        if (query.EndDate != null)
        {
            q = q.Where(x => x.CreatedAt <= query.EndDate);
        }

        return q;
    }

    private IOrderedQueryable<IGrouping<DateOnly, UserModelUsage>> GetUserModelQueryGrouped(StartEndDate query)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        IOrderedQueryable<IGrouping<DateOnly, UserModelUsage>> group = q
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.AddMinutes(query.TimezoneOffset)))
            .OrderBy(x => x.Key);
        return group;
    }
}
