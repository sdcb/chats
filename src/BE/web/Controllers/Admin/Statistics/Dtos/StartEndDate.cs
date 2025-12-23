using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.Statistics.Dtos;

public record StartEndDate
{
    [FromQuery(Name = "start")]
    public DateOnly? Start { get; init; }

    [FromQuery(Name = "end")]
    public DateOnly? End { get; init; }

    [FromQuery(Name = "tz")]
    public short TimezoneOffset { get; init; }

    internal DateTime? StartDate => Start?.ToDateTime(new TimeOnly(), DateTimeKind.Utc)
        .AddMinutes(TimezoneOffset);

    internal DateTime? EndDate => End?.ToDateTime(new TimeOnly(), DateTimeKind.Utc)
        .AddMinutes(TimezoneOffset);
}
