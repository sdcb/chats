using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Usages.Dtos;

public class UsageQueryNoPagination : IUsageQuery
{
    [FromQuery(Name = "user")]
    public string? User { get; init; }

    [FromQuery(Name = "kid")]
    public string? ApiKeyId { get; init; }

    [FromQuery(Name = "provider")]
    public string? Provider { get; init; }

    [FromQuery(Name = "model-key")]
    public string? ModelKey { get; init; }

    [FromQuery(Name = "model")]
    public string? Model { get; init; }

    [FromQuery(Name = "start")]
    public DateOnly? Start { get; init; }

    [FromQuery(Name = "end")]
    public DateOnly? End { get; init; }

    [FromQuery(Name = "source")]
    public UsageQueryType? Source { get; init; }

    [FromQuery(Name = "tz")]
    public required short TimezoneOffset { get; init; }

    public string ToExcelFileName()
    {
        string fileName = "usage";
        if (!string.IsNullOrEmpty(User))
        {
            fileName += $"_{User}";
        }
        if (!string.IsNullOrEmpty(ApiKeyId))
        {
            fileName += $"_{ApiKeyId}";
        }
        if (!string.IsNullOrEmpty(Provider))
        {
            fileName += $"_{Provider}";
        }
        if (!string.IsNullOrEmpty(ModelKey))
        {
            fileName += $"_{ModelKey}";
        }
        if (!string.IsNullOrEmpty(Model))
        {
            fileName += $"_{Model}";
        }
        if (Start.HasValue)
        {
            fileName += $"_{Start:yyyy-MM-dd}";
        }
        if (End.HasValue)
        {
            fileName += $"_{End:yyyy-MM-dd}";
        }
        if (Source.HasValue)
        {
            fileName += $"_{Source.ToString()!.ToLowerInvariant()}";
        }
        return $"{fileName}.xlsx";
    }
}
