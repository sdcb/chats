using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Controllers.Users.Usages.Dtos;

public record UsageStatistics
{
    public long TotalRequests { get; init; }
    public long SumInputTokens { get; init; }
    public long SumOutputTokens { get; init; }
    public long SumReasoningTokens { get; init; }
    public decimal SumInputCost { get; init; }
    public decimal SumOutputCost { get; init; }
    public decimal SumTotalCost => SumInputCost + SumOutputCost;
    public decimal? AvgTotalCost => TotalRequests == 0 ? null : SumTotalCost / TotalRequests;

    public double AvgPreprocessDurationMs { get; init; }
    public double AvgFirstResponseDurationMs { get; init; }
    public double AvgPostprocessDurationMs { get; init; }
    public double AvgTotalDurationMs { get; init; }

    public static async Task<UsageStatistics> FromQuery(IQueryable<UsageDto> query, CancellationToken cancellationToken)
    {
        return await query
            .GroupBy(x => 1)
            .Select(g => new UsageStatistics
            {
                TotalRequests = g.Count(),
                SumInputTokens = g.Sum(x => x.InputTokens),
                SumOutputTokens = g.Sum(x => x.OutputTokens),
                SumReasoningTokens = g.Sum(x => x.ReasoningTokens),
                SumInputCost = g.Sum(x => x.InputCost),
                SumOutputCost = g.Sum(x => x.OutputCost),
                AvgPreprocessDurationMs = g.Average(x => x.PreprocessDurationMs),
                AvgFirstResponseDurationMs = g.Average(x => x.FirstResponseDurationMs),
                AvgPostprocessDurationMs = g.Average(x => x.PostprocessDurationMs),
                AvgTotalDurationMs = g.Average(x => x.TotalDurationMs)
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new UsageStatistics
            {
                TotalRequests = 0,
                SumInputTokens = 0,
                SumOutputTokens = 0,
                SumReasoningTokens = 0,
                SumInputCost = 0,
                SumOutputCost = 0,
                AvgPreprocessDurationMs = 0,
                AvgFirstResponseDurationMs = 0,
                AvgPostprocessDurationMs = 0,
                AvgTotalDurationMs = 0
            };
    }
}