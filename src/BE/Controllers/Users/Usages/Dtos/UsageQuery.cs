using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Usages.Dtos;

public record UsageQuery : PagingRequest, IUsageQuery
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
}
