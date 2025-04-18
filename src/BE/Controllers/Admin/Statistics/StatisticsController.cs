using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.Statistics.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
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
    public async Task<ActionResult<Dictionary<string, int>>> GetModelProviderStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        return Ok(await q
            .GroupBy(x => x.UserModel.Model.ModelReference.Provider.Name)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Count(), cancellationToken));
    }

    [HttpGet("model-statistics")]
    public async Task<ActionResult<Dictionary<string, int>>> GetModelStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        return Ok(await q
            .GroupBy(x => x.UserModel.Model.ModelReference.Name)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Count(), cancellationToken));
    }

    [HttpGet("model-key-statistics")]
    public async Task<ActionResult<Dictionary<string, int>>> GetModelKeyStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        return Ok(await q
            .GroupBy(x => x.UserModel.Model.ModelKey.Name)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Count(), cancellationToken));
    }

    [HttpGet("source-statistics")]
    public async Task<ActionResult<Dictionary<string, int>>> GetSourceStatistics([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        return Ok(await q
            .GroupBy(x => x.UserApiUsage != null ? UsageQueryType.Api : UsageQueryType.Web)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Count(), cancellationToken));
    }

    [HttpGet("token-statistics-by-date")]
    public async Task<ActionResult<Dictionary<DateOnly, TokenStatisticsEntry>>> GetTokenStatisticsByDate([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        Dictionary<DateOnly, TokenStatisticsEntry> r = await q
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.AddMinutes(query.TimezoneOffset)))
            .ToDictionaryAsync(
                x => x.Key,
                x => new TokenStatisticsEntry
                {
                    InputTokens = x.Sum(y => y.InputTokens),
                    OutputTokens = x.Sum(y => y.OutputTokens),
                    ReasoningTokens = x.Sum(y => y.ReasoningTokens)
                }, cancellationToken);
        return Ok(FillMissing(r));
    }

    [HttpGet("cost-statistics-by-date")]
    public async Task<ActionResult<Dictionary<DateOnly, CostStatisticsEntry>>> GetCostStatisticsByDate([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        Dictionary<DateOnly, CostStatisticsEntry> r = await q
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.AddMinutes(query.TimezoneOffset)))
            .ToDictionaryAsync(
                x => x.Key,
                x => new CostStatisticsEntry
                {
                    InputCost = x.Sum(y => y.InputCost),
                    OutputCost = x.Sum(y => y.OutputCost),
                }, cancellationToken);
        return Ok(FillMissing(r));
    }

    [HttpGet("chat-count-by-date")]
    public async Task<ActionResult<Dictionary<DateOnly, int>>> GetChatCountByDate([FromQuery] StartEndDate query, CancellationToken cancellationToken)
    {
        IQueryable<UserModelUsage> q = GetUserModelQuery(query);
        Dictionary<DateOnly, int> r = await q
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.AddMinutes(query.TimezoneOffset)))
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Count(), cancellationToken);
        return Ok(FillMissing(r));
    }

    static Dictionary<DateOnly, T> FillMissing<T>(Dictionary<DateOnly, T> dict)
    {
        DateOnly min = dict.Keys.Min();
        DateOnly max = dict.Keys.Max();
        Dictionary<DateOnly, T> r = [];
        for (DateOnly i = min; i <= max; i = i.AddDays(1))
        {
            if (!dict.TryGetValue(i, out T? value))
            {
                r[i] = default!;
            }
            else
            {
                r[i] = value;
            }
        }
        return r;
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
}
