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
                    case RequestTraceRequestWriteModel requestItem:
                        await PersistRequestAsync(requestItem, stoppingToken);
                        break;
                    case RequestTraceResponseWriteModel responseItem:
                        await PersistResponseAsync(responseItem, stoppingToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Persist request trace failed.");
            }
        }
    }

    private async Task PersistRequestAsync(RequestTraceRequestWriteModel item, CancellationToken cancellationToken)
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
            RawRequestBodyBytes = item.RawRequestBodyBytes,
            RawResponseBodyBytes = null,
            IsRequestBodyTruncated = item.IsRequestBodyTruncated,
            IsResponseBodyTruncated = false,
            RequestTracePayload = new RequestTracePayload
            {
                RequestHeaders = item.RequestHeaders,
                ResponseHeaders = null,
                RequestBody = item.RequestBody,
                ResponseBody = null,
                RequestBodyRaw = item.RequestBodyRaw,
                ResponseBodyRaw = null,
            }
        };

        db.RequestTraces.Add(trace);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistResponseAsync(RequestTraceResponseWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        IQueryable<RequestTrace> query = db.RequestTraces
            .Include(x => x.RequestTracePayload)
            .Where(x =>
                x.Direction == (byte)item.Direction &&
                x.Method == item.Method &&
                x.Url == item.Url &&
                x.StartedAt == item.StartedAt &&
                x.DurationMs == 0 &&
                x.StatusCode == null);

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

        List<RequestTrace> matches = await query
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            logger.LogWarning("No request trace row matched response update. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
                item.Direction, item.Method, item.Url, item.StartedAt);
            return;
        }

        if (matches.Count > 1)
        {
            logger.LogWarning("Multiple request trace rows matched response update; using latest row. direction={direction}, method={method}, url={url}, startedAt={startedAt}",
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

        trace.RequestTracePayload.ResponseHeaders = item.ResponseHeaders;
        trace.RequestTracePayload.ResponseBody = item.ResponseBody;
        trace.RequestTracePayload.ResponseBodyRaw = item.ResponseBodyRaw;

        await db.SaveChangesAsync(cancellationToken);
    }
}
