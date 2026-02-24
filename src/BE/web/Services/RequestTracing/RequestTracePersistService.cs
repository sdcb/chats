using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.RequestTracing;

public sealed class RequestTracePersistService(
    IRequestTraceQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<RequestTracePersistService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (RequestTraceWriteModel item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                switch (item)
                {
                    case RequestTraceRequestHeaderWriteModel requestHeaderItem:
                        await PersistRequestHeaderAsync(requestHeaderItem, stoppingToken);
                        break;
                    case RequestTraceRequestBodyWriteModel requestBodyItem:
                        await PersistRequestBodyAsync(requestBodyItem, stoppingToken);
                        break;
                    case RequestTraceResponseHeaderWriteModel responseHeaderItem:
                        await PersistResponseHeaderAsync(responseHeaderItem, stoppingToken);
                        break;
                    case RequestTraceResponseBodyWriteModel responseBodyItem:
                        await PersistResponseBodyAsync(responseBodyItem, stoppingToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Persist request trace failed.");
            }
        }
    }

    private async Task PersistRequestHeaderAsync(RequestTraceRequestHeaderWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        RequestTrace trace = new()
        {
            StartedAt = item.StartedAt,
            DurationMs = 0,
            Direction = (byte)item.Direction,
            Source = item.Source,
            UserId = item.UserId,
            TraceId = item.TraceId,
            Method = item.Method,
            Url = item.Url,
            RequestContentType = item.RequestContentType,
            ResponseContentType = null,
            StatusCode = null,
            ErrorType = null,
            ErrorMessage = null,
            RawRequestBodyBytes = 0,
            RawResponseBodyBytes = null,
            IsRequestBodyTruncated = false,
            IsResponseBodyTruncated = false,
            RequestTracePayload = new RequestTracePayload
            {
                RequestHeaders = item.RequestHeaders,
                ResponseHeaders = null,
                RequestBody = null,
                ResponseBody = null,
                RequestBodyRaw = null,
                ResponseBodyRaw = null,
            }
        };

        db.RequestTraces.Add(trace);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistRequestBodyAsync(RequestTraceRequestBodyWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        RequestTrace? trace = await QueryCandidate(db, item)
            .Where(x => x.RequestTracePayload == null || (x.RequestTracePayload.RequestBody == null && x.RequestTracePayload.RequestBodyRaw == null))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (trace == null)
        {
            logger.LogWarning("No request trace row matched request body update. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
                item.Direction, item.Method, item.Url, item.StartedAt);
            return;
        }

        trace.RequestContentType = item.RequestContentType;
        trace.RawRequestBodyBytes = item.RawRequestBodyBytes;
        trace.IsRequestBodyTruncated = item.IsRequestBodyTruncated;

        if (trace.RequestTracePayload == null)
        {
            trace.RequestTracePayload = new RequestTracePayload
            {
                LogId = trace.Id,
                RequestHeaders = string.Empty,
            };
        }

        trace.RequestTracePayload.RequestBody = item.RequestBody;
        trace.RequestTracePayload.RequestBodyRaw = item.RequestBodyRaw;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistResponseHeaderAsync(RequestTraceResponseHeaderWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        List<RequestTrace> matches = await QueryCandidate(db, item)
            .Where(x => x.StatusCode == null && x.DurationMs == 0)
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            logger.LogWarning("No request trace row matched response header update. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
                item.Direction, item.Method, item.Url, item.StartedAt);
            return;
        }

        if (matches.Count > 1)
        {
            logger.LogWarning("Multiple request trace rows matched response header update; using latest row. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
                item.Direction, item.Method, item.Url, item.StartedAt);
        }

        RequestTrace trace = matches[0];
        trace.DurationMs = item.DurationMs;
        trace.ResponseContentType = item.ResponseContentType;
        trace.StatusCode = item.StatusCode;
        trace.ErrorType = item.ErrorType;
        trace.ErrorMessage = item.ErrorMessage;

        if (trace.RequestTracePayload == null)
        {
            trace.RequestTracePayload = new RequestTracePayload
            {
                LogId = trace.Id,
                RequestHeaders = string.Empty,
            };
        }

        trace.RequestTracePayload.ResponseHeaders = item.ResponseHeaders;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistResponseBodyAsync(RequestTraceResponseBodyWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        List<RequestTrace> matches = await QueryCandidate(db, item)
            .Where(x => x.RequestTracePayload == null || (x.RequestTracePayload.ResponseBody == null && x.RequestTracePayload.ResponseBodyRaw == null))
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            logger.LogWarning("No request trace row matched response body update. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
                item.Direction, item.Method, item.Url, item.StartedAt);
            return;
        }

        if (matches.Count > 1)
        {
            logger.LogWarning("Multiple request trace rows matched response body update; using latest row. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
                item.Direction, item.Method, item.Url, item.StartedAt);
        }

        RequestTrace trace = matches[0];
        trace.DurationMs = item.DurationMs;
        trace.ResponseContentType = item.ResponseContentType;
        trace.StatusCode = item.StatusCode;
        trace.ErrorType = item.ErrorType;
        trace.ErrorMessage = item.ErrorMessage;
        trace.RawResponseBodyBytes = item.RawResponseBodyBytes;
        trace.IsResponseBodyTruncated = item.IsResponseBodyTruncated;

        if (trace.RequestTracePayload == null)
        {
            trace.RequestTracePayload = new RequestTracePayload
            {
                LogId = trace.Id,
                RequestHeaders = string.Empty,
            };
        }

        trace.RequestTracePayload.ResponseBody = item.ResponseBody;
        trace.RequestTracePayload.ResponseBodyRaw = item.ResponseBodyRaw;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<RequestTrace> QueryCandidate(ChatsDB db, RequestTraceWriteModel item)
    {
        IQueryable<RequestTrace> query = db.RequestTraces
            .Include(x => x.RequestTracePayload)
            .Where(x =>
                x.Direction == (byte)item.Direction &&
                x.Method == item.Method &&
                x.Url == item.Url &&
                x.StartedAt == item.StartedAt);

        query = item.UserId.HasValue
            ? query.Where(x => x.UserId == item.UserId)
            : query.Where(x => x.UserId == null);

        if (item.TraceId == null)
        {
            query = query.Where(x => x.TraceId == null);
        }
        else
        {
            query = query.Where(x => x.TraceId == item.TraceId);
        }

        if (item.Source == null)
        {
            query = query.Where(x => x.Source == null);
        }
        else
        {
            query = query.Where(x => x.Source == item.Source);
        }

        return query;
    }
}
