using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.SecurityLogs.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;

namespace Chats.BE.Controllers.Admin.SecurityLogs;

[Route("api/admin/security-logs"), AuthorizeAdmin]
public class SecurityLogsController(ChatsDB db) : ControllerBase
{
    private const int ExportLimit = 5000;

    [HttpGet("password-attempts")]
    public async Task<ActionResult<PagedResult<PasswordAttemptDto>>> GetPasswordAttempts([FromQuery] SecurityLogQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<PasswordAttemptDto> rows = FilterPasswordAttempts(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new PasswordAttemptDto
            {
                Id = x.Id,
                UserName = x.UserName,
                UserId = x.UserId,
                MatchedUserName = x.User != null ? x.User.UserName : null,
                IsSuccessful = x.IsSuccessful,
                FailureReason = x.FailureReason,
                Ip = x.ClientInfo.ClientIp.Ipaddress,
                UserAgent = x.ClientInfo.ClientUserAgent.UserAgent,
                CreatedAt = x.CreatedAt
            });

        return Ok(await PagedResult.FromQuery(rows, query, cancellationToken));
    }

    [HttpGet("password-attempts/export")]
    public async Task<IActionResult> ExportPasswordAttempts([FromQuery] SecurityLogExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        List<PasswordAttemptDto> rows = await FilterPasswordAttempts(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new PasswordAttemptDto
            {
                Id = x.Id,
                UserName = x.UserName,
                UserId = x.UserId,
                MatchedUserName = x.User != null ? x.User.UserName : null,
                IsSuccessful = x.IsSuccessful,
                FailureReason = x.FailureReason,
                Ip = x.ClientInfo.ClientIp.Ipaddress,
                UserAgent = x.ClientInfo.ClientUserAgent.UserAgent,
                CreatedAt = x.CreatedAt
            })
            .Take(ExportLimit)
            .ToListAsync(cancellationToken);

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, rows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"password-attempts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpDelete("password-attempts")]
    public async Task<ActionResult<int>> ClearPasswordAttempts([FromBody] SecurityLogExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int deleted = await FilterPasswordAttempts(query, asNoTracking: false)
            .ExecuteDeleteAsync(cancellationToken);

        return Ok(deleted);
    }

    [HttpGet("keycloak-attempts")]
    public async Task<ActionResult<PagedResult<KeycloakAttemptDto>>> GetKeycloakAttempts([FromQuery] SecurityLogQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<KeycloakAttemptDto> rows = FilterKeycloakAttempts(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new KeycloakAttemptDto
            {
                Id = x.Id,
                Provider = x.Provider,
                Sub = x.Sub,
                Email = x.Email,
                UserId = x.UserId,
                UserName = x.User != null ? x.User.UserName : null,
                IsSuccessful = x.IsSuccessful,
                FailureReason = x.FailureReason,
                Ip = x.ClientInfo.ClientIp.Ipaddress,
                UserAgent = x.ClientInfo.ClientUserAgent.UserAgent,
                CreatedAt = x.CreatedAt
            });

        return Ok(await PagedResult.FromQuery(rows, query, cancellationToken));
    }

    [HttpGet("keycloak-attempts/export")]
    public async Task<IActionResult> ExportKeycloakAttempts([FromQuery] SecurityLogExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        List<KeycloakAttemptDto> rows = await FilterKeycloakAttempts(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new KeycloakAttemptDto
            {
                Id = x.Id,
                Provider = x.Provider,
                Sub = x.Sub,
                Email = x.Email,
                UserId = x.UserId,
                UserName = x.User != null ? x.User.UserName : null,
                IsSuccessful = x.IsSuccessful,
                FailureReason = x.FailureReason,
                Ip = x.ClientInfo.ClientIp.Ipaddress,
                UserAgent = x.ClientInfo.ClientUserAgent.UserAgent,
                CreatedAt = x.CreatedAt
            })
            .Take(ExportLimit)
            .ToListAsync(cancellationToken);

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, rows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"keycloak-attempts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpDelete("keycloak-attempts")]
    public async Task<ActionResult<int>> ClearKeycloakAttempts([FromBody] SecurityLogExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int deleted = await FilterKeycloakAttempts(query, asNoTracking: false)
            .ExecuteDeleteAsync(cancellationToken);

        return Ok(deleted);
    }

    [HttpGet("sms-attempts")]
    public async Task<ActionResult<PagedResult<SmsAttemptDto>>> GetSmsAttempts([FromQuery] SecurityLogQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<SmsAttemptDto> rows = FilterSmsAttempts(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new SmsAttemptDto
            {
                Id = x.Id,
                PhoneNumber = x.SmsRecord.PhoneNumber,
                Code = x.Code,
                UserId = x.SmsRecord.UserId,
                UserName = x.SmsRecord.User != null ? x.SmsRecord.User.UserName : null,
                Type = x.SmsRecord.Type.Name,
                Status = x.SmsRecord.Status.Name,
                Ip = x.ClientInfo.ClientIp.Ipaddress,
                UserAgent = x.ClientInfo.ClientUserAgent.UserAgent,
                CreatedAt = x.CreatedAt
            });

        return Ok(await PagedResult.FromQuery(rows, query, cancellationToken));
    }

    [HttpGet("sms-attempts/export")]
    public async Task<IActionResult> ExportSmsAttempts([FromQuery] SecurityLogExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        List<SmsAttemptDto> rows = await FilterSmsAttempts(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new SmsAttemptDto
            {
                Id = x.Id,
                PhoneNumber = x.SmsRecord.PhoneNumber,
                Code = x.Code,
                UserId = x.SmsRecord.UserId,
                UserName = x.SmsRecord.User != null ? x.SmsRecord.User.UserName : null,
                Type = x.SmsRecord.Type.Name,
                Status = x.SmsRecord.Status.Name,
                Ip = x.ClientInfo.ClientIp.Ipaddress,
                UserAgent = x.ClientInfo.ClientUserAgent.UserAgent,
                CreatedAt = x.CreatedAt
            })
            .Take(ExportLimit)
            .ToListAsync(cancellationToken);

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, rows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"sms-attempts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpDelete("sms-attempts")]
    public async Task<ActionResult<int>> ClearSmsAttempts([FromBody] SecurityLogExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int deleted = await FilterSmsAttempts(query, asNoTracking: false)
            .ExecuteDeleteAsync(cancellationToken);

        return Ok(deleted);
    }

    private IQueryable<PasswordAttempt> FilterPasswordAttempts(ISecurityLogFilter query, bool asNoTracking = true)
    {
        IQueryable<PasswordAttempt> source = asNoTracking ? db.PasswordAttempts.AsNoTracking() : db.PasswordAttempts;

        if (query.Start != null)
        {
            DateTime localStart = DateOnly.FromDateTime(query.Start.Value)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.CreatedAt >= localStart);
        }

        if (query.End != null)
        {
            DateTime localEnd = DateOnly.FromDateTime(query.End.Value)
                .AddDays(1)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.CreatedAt < localEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            string keyword = query.UserName.Trim();
            source = source.Where(x => EF.Functions.Like(x.UserName, $"%{keyword}%") || (x.User != null && EF.Functions.Like(x.User.UserName, $"%{keyword}%")));
        }

        return source;
    }

    private IQueryable<KeycloakAttempt> FilterKeycloakAttempts(ISecurityLogFilter query, bool asNoTracking = true)
    {
        IQueryable<KeycloakAttempt> source = asNoTracking ? db.KeycloakAttempts.AsNoTracking() : db.KeycloakAttempts;

        if (query.Start != null)
        {
            DateTime localStart = DateOnly.FromDateTime(query.Start.Value)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.CreatedAt >= localStart);
        }

        if (query.End != null)
        {
            DateTime localEnd = DateOnly.FromDateTime(query.End.Value)
                .AddDays(1)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.CreatedAt < localEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            string keyword = query.UserName.Trim();
            source = source.Where(x =>
                (x.User != null && EF.Functions.Like(x.User.UserName, $"%{keyword}%")) ||
                (x.Email != null && EF.Functions.Like(x.Email, $"%{keyword}%")));
        }

        return source;
    }

    private IQueryable<SmsAttempt> FilterSmsAttempts(ISecurityLogFilter query, bool asNoTracking = true)
    {
        IQueryable<SmsAttempt> source = asNoTracking ? db.SmsAttempts.AsNoTracking() : db.SmsAttempts;

        if (query.Start != null)
        {
            DateTime localStart = DateOnly.FromDateTime(query.Start.Value)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.CreatedAt >= localStart);
        }

        if (query.End != null)
        {
            DateTime localEnd = DateOnly.FromDateTime(query.End.Value)
                .AddDays(1)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.CreatedAt < localEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            string keyword = query.UserName.Trim();
            source = source.Where(x => x.SmsRecord.User != null && EF.Functions.Like(x.SmsRecord.User.UserName, $"%{keyword}%"));
        }

        return source;
    }


}
