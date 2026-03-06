using System.Net;
using System.Text;

using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.RequestTrace.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using DBRequestTrace = Chats.DB.RequestTrace;

namespace Chats.BE.Controllers.Admin.RequestTrace;

[Route("api/admin/request-trace"), AuthorizeAdmin]
public class RequestTraceController(ChatsDB db) : ControllerBase
{
    private const int ExportLimit = 10000;

    [HttpGet]
    public async Task<ActionResult<PagedResult<RequestTraceListItemDto>>> GetList([FromQuery] RequestTraceQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<RequestTraceListItemDto> rows = BuildListQuery(query)
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id);

        return Ok(await PagedResult.FromQuery(rows, query, cancellationToken));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] RequestTraceExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        RequestTraceListItemDto[] rows = await BuildListQuery(query)
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Take(ExportLimit)
            .ToArrayAsync(cancellationToken);

        HashSet<string>? selectedColumns = ParseColumnSet(query.Columns);
        List<Dictionary<string, object?>> exportRows = rows.Select(x => BuildExportRow(x, selectedColumns)).ToList();

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"request-trace-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpDelete]
    public async Task<ActionResult<int>> DeleteByQuery([FromBody] RequestTraceExportQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Guid[] ids = await FilterRequestTraceEntity(query)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        if (ids.Length == 0)
        {
            return Ok(0);
        }

        await db.RequestTracePayloads
            .Where(x => ids.Contains(x.LogId))
            .ExecuteDeleteAsync(cancellationToken);

        int deleted = await db.RequestTraces
            .Where(x => ids.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);

        return Ok(deleted);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequestTraceDetailsDto>> GetDetails([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        RequestTraceDetailsDto? details = await BuildDetailsQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (details == null)
        {
            return NotFound();
        }

        return Ok(details);
    }

    [HttpGet("{id:guid}/dump")]
    public async Task<IActionResult> DownloadDump([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        RequestTraceDetailsDto? details = await BuildDetailsQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (details == null)
        {
            return NotFound();
        }

        if (details.HasResponseBodyRaw && string.IsNullOrWhiteSpace(details.ResponseBody))
        {
            return BadRequest("Response body is binary-only. Please download raw content.");
        }

        string requestHeaders = (details.RequestHeaders ?? string.Empty).TrimEnd('\r', '\n');
        string responseHeaders = (details.ResponseHeaders ?? string.Empty).TrimEnd('\r', '\n');
        string requestBody = details.RequestBody ?? string.Empty;
        string responseBody = details.ResponseBody ?? string.Empty;

        short statusCode = details.StatusCode ?? 0;
        string reasonPhrase = Enum.IsDefined(typeof(HttpStatusCode), (int)statusCode)
            ? ((HttpStatusCode)statusCode).ToString().Replace('_', ' ')
            : "Unknown";

        StringBuilder dump = new();
        dump.Append(details.Method).Append(' ').Append(details.Url).Append(" HTTP/1.1\r\n");
        if (!string.IsNullOrWhiteSpace(requestHeaders))
        {
            dump.Append(requestHeaders).Append("\r\n");
        }

        dump.Append("\r\n");
        dump.Append(requestBody).Append("\r\n");

        dump.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reasonPhrase).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(responseHeaders))
        {
            dump.Append(responseHeaders).Append("\r\n");
        }

        dump.Append("\r\n");
        dump.Append(responseBody);

        byte[] bytes = Encoding.UTF8.GetBytes(dump.ToString());
        return File(bytes, "application/octet-stream", $"request-trace-{id}.dump");
    }

    [HttpGet("{id:guid}/raw")]
    public async Task<IActionResult> DownloadRaw([FromRoute] Guid id, [FromQuery] string part, CancellationToken cancellationToken)
    {
        RequestTracePayload? payload = await db.RequestTracePayloads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LogId == id, cancellationToken);

        if (payload == null)
        {
            return NotFound();
        }

        bool isRequest = string.Equals(part, "request", StringComparison.OrdinalIgnoreCase);
        bool isResponse = string.Equals(part, "response", StringComparison.OrdinalIgnoreCase);

        if (!isRequest && !isResponse)
        {
            return BadRequest("part must be request or response");
        }

        byte[]? bytes = isRequest ? payload.RequestBodyRaw : payload.ResponseBodyRaw;
        if (bytes == null || bytes.Length == 0)
        {
            return NotFound();
        }

        string fileName = isRequest
            ? $"request-trace-{id}-request.raw"
            : $"request-trace-{id}-response.raw";
        return File(bytes, "application/octet-stream", fileName);
    }

    private IQueryable<RequestTraceListItemDto> BuildListQuery(IRequestTraceFilter query)
    {
        IQueryable<DBRequestTrace> source = FilterRequestTraceEntity(query);

        return from trace in source
               join user in db.Users.AsNoTracking() on trace.UserId equals user.Id into users
               from user in users.DefaultIfEmpty()
               join payload in db.RequestTracePayloads.AsNoTracking() on trace.Id equals payload.LogId into payloads
               from payload in payloads.DefaultIfEmpty()
               select new RequestTraceListItemDto
               {
                   Id = trace.Id,
                   StartedAt = trace.StartedAt,
                   RequestBodyAt = trace.RequestBodyAt,
                   ResponseHeaderAt = trace.ResponseHeaderAt,
                   ResponseBodyAt = trace.ResponseBodyAt,
                   Direction = trace.Direction,
                   Source = trace.Source,
                   UserId = trace.UserId,
                   UserName = user != null ? user.UserName : null,
                   TraceId = trace.TraceId,
                   Method = trace.Method,
                   Url = trace.Url,
                   RequestContentType = trace.RequestContentType,
                   ResponseContentType = trace.ResponseContentType,
                   StatusCode = trace.StatusCode,
                   ErrorType = trace.ErrorType,
                   ErrorMessage = payload != null ? payload.ErrorMessage : null,
                   RawRequestBodyBytes = trace.RawRequestBodyBytes,
                   RawResponseBodyBytes = trace.RawResponseBodyBytes,
                   RequestBodyLength = trace.RequestBodyLength,
                   ResponseBodyLength = trace.ResponseBodyLength,
                   HasPayload = payload != null,
                   HasRequestBodyRaw = payload != null && payload.RequestBodyRaw != null,
                   HasResponseBodyRaw = payload != null && payload.ResponseBodyRaw != null,
               };
    }

    private IQueryable<RequestTraceDetailsDto> BuildDetailsQuery()
    {
        return from trace in db.RequestTraces.AsNoTracking()
               join user in db.Users.AsNoTracking() on trace.UserId equals user.Id into users
               from user in users.DefaultIfEmpty()
               join payload in db.RequestTracePayloads.AsNoTracking() on trace.Id equals payload.LogId into payloads
               from payload in payloads.DefaultIfEmpty()
               select new RequestTraceDetailsDto
               {
                   Id = trace.Id,
                   StartedAt = trace.StartedAt,
                   RequestBodyAt = trace.RequestBodyAt,
                   ResponseHeaderAt = trace.ResponseHeaderAt,
                   ResponseBodyAt = trace.ResponseBodyAt,
                   Direction = trace.Direction,
                   Source = trace.Source,
                   UserId = trace.UserId,
                   UserName = user != null ? user.UserName : null,
                   TraceId = trace.TraceId,
                   Method = trace.Method,
                   Url = trace.Url,
                   RequestContentType = trace.RequestContentType,
                   ResponseContentType = trace.ResponseContentType,
                   StatusCode = trace.StatusCode,
                   ErrorType = trace.ErrorType,
                   ErrorMessage = payload != null ? payload.ErrorMessage : null,
                   RawRequestBodyBytes = trace.RawRequestBodyBytes,
                   RawResponseBodyBytes = trace.RawResponseBodyBytes,
                   RequestBodyLength = trace.RequestBodyLength,
                   ResponseBodyLength = trace.ResponseBodyLength,
                   HasPayload = payload != null,
                   HasRequestBodyRaw = payload != null && payload.RequestBodyRaw != null,
                   HasResponseBodyRaw = payload != null && payload.ResponseBodyRaw != null,
                   RequestHeaders = payload != null ? payload.RequestHeaders : null,
                   ResponseHeaders = payload != null ? payload.ResponseHeaders : null,
                   RequestBody = payload != null ? payload.RequestBody : null,
                   ResponseBody = payload != null ? payload.ResponseBody : null,
               };
    }

    private IQueryable<DBRequestTrace> FilterRequestTraceEntity(IRequestTraceFilter query)
    {
        IQueryable<DBRequestTrace> source = db.RequestTraces.AsNoTracking();

        if (query.Start != null)
        {
            DateTime localStart = DateOnly.FromDateTime(query.Start.Value)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.StartedAt >= localStart);
        }

        if (query.End != null)
        {
            DateTime localEnd = DateOnly.FromDateTime(query.End.Value)
                .AddDays(1)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            source = source.Where(x => x.StartedAt < localEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.Url))
        {
            string keyword = query.Url.Trim();
            source = source.Where(x => EF.Functions.Like(x.Url, $"%{keyword}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.TraceId))
        {
            string keyword = query.TraceId.Trim();
            source = source.Where(x => x.TraceId != null && EF.Functions.Like(x.TraceId, $"%{keyword}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            string keyword = query.UserName.Trim();
            source = source.Where(x => x.UserId != null && db.Users.Any(u => u.Id == x.UserId && EF.Functions.Like(u.UserName, $"%{keyword}%")));
        }

        if (query.Direction != null)
        {
            source = source.Where(x => x.Direction == query.Direction.Value);
        }

        return source;
    }

    private static HashSet<string>? ParseColumnSet(string? columns)
    {
        if (string.IsNullOrWhiteSpace(columns))
        {
            return null;
        }

        return columns
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> BuildExportRow(RequestTraceListItemDto row, HashSet<string>? selectedColumns)
    {
        static bool Include(HashSet<string>? set, string key)
            => set == null || set.Contains(key);

        Dictionary<string, object?> export = new();

        if (Include(selectedColumns, "id")) export["id"] = row.Id;
        if (Include(selectedColumns, "startedAt")) export["startedAt"] = row.StartedAt;
        if (Include(selectedColumns, "requestBodyAt")) export["requestBodyAt"] = row.RequestBodyAt;
        if (Include(selectedColumns, "responseHeaderAt")) export["responseHeaderAt"] = row.ResponseHeaderAt;
        if (Include(selectedColumns, "responseBodyAt")) export["responseBodyAt"] = row.ResponseBodyAt;
        if (Include(selectedColumns, "direction")) export["direction"] = row.Direction;
        if (Include(selectedColumns, "source")) export["source"] = row.Source;
        if (Include(selectedColumns, "userName")) export["userName"] = row.UserName;
        if (Include(selectedColumns, "traceId")) export["traceId"] = row.TraceId;
        if (Include(selectedColumns, "method")) export["method"] = row.Method;
        if (Include(selectedColumns, "url")) export["url"] = row.Url;
        if (Include(selectedColumns, "requestContentType")) export["requestContentType"] = row.RequestContentType;
        if (Include(selectedColumns, "responseContentType")) export["responseContentType"] = row.ResponseContentType;
        if (Include(selectedColumns, "statusCode")) export["statusCode"] = row.StatusCode;
        if (Include(selectedColumns, "errorType")) export["errorType"] = row.ErrorType;
        if (Include(selectedColumns, "errorMessage")) export["errorMessage"] = row.ErrorMessage;
        if (Include(selectedColumns, "rawRequestBodyBytes")) export["rawRequestBodyBytes"] = row.RawRequestBodyBytes;
        if (Include(selectedColumns, "rawResponseBodyBytes")) export["rawResponseBodyBytes"] = row.RawResponseBodyBytes;
        if (Include(selectedColumns, "requestBodyLength")) export["requestBodyLength"] = row.RequestBodyLength;
        if (Include(selectedColumns, "responseBodyLength")) export["responseBodyLength"] = row.ResponseBodyLength;

        return export;
    }

}
