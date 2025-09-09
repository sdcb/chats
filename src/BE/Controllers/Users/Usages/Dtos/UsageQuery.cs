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
}
