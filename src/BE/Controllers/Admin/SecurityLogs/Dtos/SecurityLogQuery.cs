using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.SecurityLogs.Dtos;

public interface ISecurityLogFilter
{
    DateTime? Start { get; }

    DateTime? End { get; }

    string? UserName { get; }
}

public record SecurityLogQuery : PagingRequest, ISecurityLogFilter
{
    [DefaultValue(null)]
    [FromQuery(Name = "start")]
    public DateTime? Start { get; init; }

    [DefaultValue(null)]
    [FromQuery(Name = "end")]
    public DateTime? End { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "username")]
    public string? UserName { get; init; }
}

public record SecurityLogExportQuery : ISecurityLogFilter
{
    [FromQuery(Name = "start")]
    public DateTime? Start { get; init; }

    [FromQuery(Name = "end")]
    public DateTime? End { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "username")]
    public string? UserName { get; init; }
}
