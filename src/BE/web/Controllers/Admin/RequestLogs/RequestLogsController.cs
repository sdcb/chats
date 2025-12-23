using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.RequestLogs.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.RequestLogs;

[Route("api/admin/request-logs"), AuthorizeAdmin]
public class RequestLogsController : ControllerBase
{
    [HttpPost]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:删除未使用的参数", Justification = "<挂起>")]
    public ActionResult<PagedResult<RequestLogDto>> GetRequestLogs([FromBody] QueryPagingRequest pagingRequest)
    {
        //IQueryable<RequestLogDto> query = db.RequestLogs
        //    .Select(x => new RequestLogDto()
        //    {
        //        Id = x.Id,
        //        Ip = x.Ip,
        //        Method = x.Method,
        //        Url = x.Url,
        //        StatusCode = x.StatusCode,
        //        Username = x.User!.Username,
        //        CreatedAt = x.CreatedAt
        //    })
        //    .OrderByDescending(x => x.CreatedAt)
        //    .AsQueryable();
        //if (!string.IsNullOrWhiteSpace(pagingRequest.Query))
        //{
        //    query = query.Where(x => x.Username == pagingRequest.Query);
        //}

        //return Ok(await PagedResult.FromQuery(query, pagingRequest, cancellationToken));

        return Ok(new PagedResult<RequestLogDto>()
        {
            Count = 0,
            Rows = []
        });
    }

    [HttpGet]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:删除未使用的参数", Justification = "<挂起>")]
    public ActionResult<LogEntry> GetRequestLogDetails([FromQuery] Guid id)
    {
        return NotFound();
        //LogEntry? requestLog = await db.RequestLogs
        //    .Where(x => x.Id == id)
        //    .Select(x => new LogEntry()
        //    {
        //        Id = x.Id,
        //        Ip = x.Ip,
        //        Method = x.Method,
        //        Url = x.Url,
        //        StatusCode = x.StatusCode,
        //        User = x.UserId != null ? new OnlyUserName { Username = x.User!.Username } : null,
        //        CreatedAt = x.CreatedAt,
        //        Request = x.Request,
        //        Response = x.Response,
        //        Headers = x.Headers,
        //        RequestTime = x.RequestTime,
        //        ResponseTime = x.ResponseTime,
        //        UserId = x.UserId,
        //    })
        //    .FirstOrDefaultAsync(cancellationToken);
        //if (requestLog == null)
        //{
        //    return NotFound();
        //}

        //return Ok(requestLog);
    }
}
