using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Usages.Dtos;

public record UsageQuery : PagingRequest
{
    [FromQuery(Name = "user")]
    public string? User { get; init; }

    [FromQuery(Name = "kid")]
    public string? ApiKeyId { get; init; }

    [FromQuery(Name = "provider")]
    public string? Provider { get; init; }

    [FromQuery(Name = "start")]
    public DateTime? Start { get; init; }

    [FromQuery(Name = "end")]
    public DateTime? End { get; init; }

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
        if (Start.HasValue)
        {
            fileName += $"_{Start:yyyy-MM-dd}";
        }
        if (End.HasValue)
        {
            fileName += $"_{End:yyyy-MM-dd}";
        }
        return $"{fileName}.xlsx";
    }
}
