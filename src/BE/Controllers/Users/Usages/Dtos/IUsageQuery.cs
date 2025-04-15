using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Usages.Dtos;

public interface IUsageQuery
{
    [FromQuery(Name = "user")]
    public string? User { get; }

    [FromQuery(Name = "kid")]
    public string? ApiKeyId { get; }

    [FromQuery(Name = "provider")]
    public string? Provider { get;   }

    [FromQuery(Name = "start")]
    public DateTime? Start { get; }

    [FromQuery(Name = "end")]
    public DateTime? End { get; }

    [FromQuery(Name = "tz")]
    public short TimezoneOffset { get; }
}
