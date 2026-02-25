using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.RequestTrace.Dtos;

public interface IRequestTraceFilter
{
    DateTime? Start { get; }

    DateTime? End { get; }

    string? Url { get; }

    string? TraceId { get; }

    string? UserName { get; }

    byte? Direction { get; }

    short TimezoneOffset { get; }
}

public record RequestTraceQuery : PagingRequest, IRequestTraceFilter
{
    [DefaultValue(null)]
    [FromQuery(Name = "start")]
    public DateTime? Start { get; init; }

    [DefaultValue(null)]
    [FromQuery(Name = "end")]
    public DateTime? End { get; init; }

    [StringLength(2048)]
    [FromQuery(Name = "url")]
    public string? Url { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "traceId")]
    public string? TraceId { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "username")]
    public string? UserName { get; init; }

    [FromQuery(Name = "direction")]
    public byte? Direction { get; init; }

    [FromQuery(Name = "tz")]
    public short TimezoneOffset { get; init; }
}

public record RequestTraceExportQuery : IRequestTraceFilter
{
    [FromQuery(Name = "start")]
    public DateTime? Start { get; init; }

    [FromQuery(Name = "end")]
    public DateTime? End { get; init; }

    [StringLength(2048)]
    [FromQuery(Name = "url")]
    public string? Url { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "traceId")]
    public string? TraceId { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "username")]
    public string? UserName { get; init; }

    [FromQuery(Name = "direction")]
    public byte? Direction { get; init; }

    [FromQuery(Name = "tz")]
    public short TimezoneOffset { get; init; }

    [FromQuery(Name = "columns")]
    public string? Columns { get; init; }
}
