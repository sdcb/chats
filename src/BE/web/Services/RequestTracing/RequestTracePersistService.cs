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
                await PersistSingleAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Persist request trace failed.");
            }
        }
    }

    internal async Task PersistSingleAsync(RequestTraceWriteModel item, CancellationToken cancellationToken)
    {
        switch (item)
        {
            case RequestTraceRequestHeaderWriteModel requestHeaderItem:
                await PersistRequestHeaderAsync(requestHeaderItem, cancellationToken);
                break;
            case RequestTraceRequestBodyWriteModel requestBodyItem:
                await PersistRequestBodyAsync(requestBodyItem, cancellationToken);
                break;
            case RequestTraceResponseHeaderWriteModel responseHeaderItem:
                await PersistResponseHeaderAsync(responseHeaderItem, cancellationToken);
                break;
            case RequestTraceResponseBodyWriteModel responseBodyItem:
                await PersistResponseBodyAsync(responseBodyItem, cancellationToken);
                break;
            case RequestTraceExceptionWriteModel exceptionItem:
                await PersistExceptionAsync(exceptionItem, cancellationToken);
                break;
        }
    }

    private async Task PersistRequestHeaderAsync(RequestTraceRequestHeaderWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        RequestTrace trace = new()
        {
            Id = item.LogId,
            StartedAt = item.StartedAt,
            RequestBodyAt = null,
            ResponseHeaderAt = null,
            ResponseBodyAt = null,
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
                LogId = item.LogId,
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

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            RequestTrace? trace = await db.RequestTraces
                .Include(x => x.RequestTracePayload)
                .FirstOrDefaultAsync(x => x.Id == item.LogId, cancellationToken);

            if (trace == null)
            {
                logger.LogWarning("No request trace row matched request body update. logId={logId}", item.LogId);
                return;
            }

            trace.RequestContentType = item.RequestContentType;
            trace.RequestBodyAt = item.RequestBodyAt;
            trace.RawRequestBodyBytes = item.RawRequestBodyBytes;
            trace.IsRequestBodyTruncated = item.IsRequestBodyTruncated;

            if (trace.RequestTracePayload == null)
            {
                trace.RequestTracePayload = new RequestTracePayload
                {
                    LogId = item.LogId,
                    RequestHeaders = string.Empty,
                };
            }

            trace.RequestTracePayload.RequestBody = item.RequestBody;
            trace.RequestTracePayload.RequestBodyRaw = item.RequestBodyRaw;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        int traceUpdated = await db.RequestTraces
            .Where(x => x.Id == item.LogId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(v => v.RequestContentType, item.RequestContentType)
                .SetProperty(v => v.RequestBodyAt, item.RequestBodyAt)
                .SetProperty(v => v.RawRequestBodyBytes, item.RawRequestBodyBytes)
                .SetProperty(v => v.IsRequestBodyTruncated, item.IsRequestBodyTruncated), cancellationToken);

        if (traceUpdated == 0)
        {
            logger.LogWarning("No request trace row matched request body update. logId={logId}", item.LogId);
            return;
        }

        if (item.RequestBody != null || item.RequestBodyRaw != null)
        {
            await db.RequestTracePayloads
                .Where(x => x.LogId == item.LogId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(v => v.RequestBody, item.RequestBody)
                    .SetProperty(v => v.RequestBodyRaw, item.RequestBodyRaw), cancellationToken);
        }
    }

    private async Task PersistResponseHeaderAsync(RequestTraceResponseHeaderWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            RequestTrace? trace = await db.RequestTraces
                .Include(x => x.RequestTracePayload)
                .FirstOrDefaultAsync(x => x.Id == item.LogId, cancellationToken);

            if (trace == null)
            {
                logger.LogWarning("No request trace row matched response header update. logId={logId}", item.LogId);
                return;
            }

            trace.ResponseHeaderAt = item.ResponseHeaderAt;
            trace.ResponseContentType = item.ResponseContentType;
            trace.StatusCode = item.StatusCode;
            trace.ErrorType = item.ErrorType;
            trace.ErrorMessage = item.ErrorMessage;

            if (trace.RequestTracePayload == null)
            {
                trace.RequestTracePayload = new RequestTracePayload
                {
                    LogId = item.LogId,
                    RequestHeaders = string.Empty,
                };
            }

            trace.RequestTracePayload.ResponseHeaders = item.ResponseHeaders;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        int traceUpdated = await db.RequestTraces
            .Where(x => x.Id == item.LogId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(v => v.ResponseHeaderAt, item.ResponseHeaderAt)
                .SetProperty(v => v.ResponseContentType, item.ResponseContentType)
                .SetProperty(v => v.StatusCode, item.StatusCode)
                .SetProperty(v => v.ErrorType, item.ErrorType)
                .SetProperty(v => v.ErrorMessage, item.ErrorMessage), cancellationToken);

        if (traceUpdated == 0)
        {
            logger.LogWarning("No request trace row matched response header update. logId={logId}", item.LogId);
            return;
        }

        await db.RequestTracePayloads
            .Where(x => x.LogId == item.LogId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(v => v.ResponseHeaders, item.ResponseHeaders), cancellationToken);
    }

    private async Task PersistResponseBodyAsync(RequestTraceResponseBodyWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            RequestTrace? trace = await db.RequestTraces
                .Include(x => x.RequestTracePayload)
                .FirstOrDefaultAsync(x => x.Id == item.LogId, cancellationToken);

            if (trace == null)
            {
                logger.LogWarning("No request trace row matched response body update. logId={logId}", item.LogId);
                return;
            }

            trace.ResponseBodyAt = item.ResponseBodyAt;
            trace.ResponseContentType = item.ResponseContentType;
            trace.StatusCode = item.StatusCode;
            trace.RawResponseBodyBytes = item.RawResponseBodyBytes;
            trace.IsResponseBodyTruncated = item.IsResponseBodyTruncated;

            if (trace.RequestTracePayload == null)
            {
                trace.RequestTracePayload = new RequestTracePayload
                {
                    LogId = item.LogId,
                    RequestHeaders = string.Empty,
                };
            }

            trace.RequestTracePayload.ResponseBody = item.ResponseBody;
            trace.RequestTracePayload.ResponseBodyRaw = item.ResponseBodyRaw;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        int traceUpdated = await db.RequestTraces
            .Where(x => x.Id == item.LogId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(v => v.ResponseBodyAt, item.ResponseBodyAt)
                .SetProperty(v => v.ResponseContentType, item.ResponseContentType)
                .SetProperty(v => v.StatusCode, item.StatusCode)
                .SetProperty(v => v.RawResponseBodyBytes, item.RawResponseBodyBytes)
                .SetProperty(v => v.IsResponseBodyTruncated, item.IsResponseBodyTruncated), cancellationToken);

        if (traceUpdated == 0)
        {
            logger.LogWarning("No request trace row matched response body update. logId={logId}", item.LogId);
            return;
        }

        if (item.ResponseBody != null || item.ResponseBodyRaw != null)
        {
            await db.RequestTracePayloads
                .Where(x => x.LogId == item.LogId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(v => v.ResponseBody, item.ResponseBody)
                    .SetProperty(v => v.ResponseBodyRaw, item.ResponseBodyRaw), cancellationToken);
        }
    }

    private async Task PersistExceptionAsync(RequestTraceExceptionWriteModel item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            RequestTrace? trace = await db.RequestTraces
                .FirstOrDefaultAsync(x => x.Id == item.LogId, cancellationToken);

            if (trace == null)
            {
                logger.LogWarning("No request trace row matched exception update. logId={logId}", item.LogId);
                return;
            }

            trace.ResponseContentType ??= item.ResponseContentType;
            trace.StatusCode ??= item.StatusCode;
            trace.ErrorType = item.ErrorType;
            trace.ErrorMessage = item.ErrorMessage;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        int updated = await db.RequestTraces
            .Where(x => x.Id == item.LogId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(v => v.ResponseContentType, v => v.ResponseContentType ?? item.ResponseContentType)
                .SetProperty(v => v.StatusCode, v => v.StatusCode ?? item.StatusCode)
                .SetProperty(v => v.ErrorType, item.ErrorType)
                .SetProperty(v => v.ErrorMessage, item.ErrorMessage), cancellationToken);

        if (updated == 0)
        {
            logger.LogWarning("No request trace row matched exception update. logId={logId}", item.LogId);
        }
    }
}
