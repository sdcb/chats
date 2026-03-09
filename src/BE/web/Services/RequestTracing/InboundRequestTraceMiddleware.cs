using Chats.BE.Services.Configs;
using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;

namespace Chats.BE.Services.RequestTracing;

public sealed class InboundRequestTraceMiddleware(
    RequestDelegate next,
    IRequestTraceConfigProvider configProvider,
    IRequestTraceQueue queue,
    IUrlEncryptionService idEncryption,
    ILogger<InboundRequestTraceMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        RequestTraceConfig config = configProvider.GetInboundConfig();
        if (!RequestTraceHelper.IsEnabledAndSampled(config))
        {
            await next(context);
            return;
        }

        Guid logId = Guid.CreateVersion7();
        DateTime startedAt = DateTime.UtcNow;
        long startTick = Stopwatch.GetTimestamp();
        string method = context.Request.Method;
        string rawUrl = context.Request.Path + context.Request.QueryString;
        string redactedUrl = RequestTraceHelper.RedactUrlQueryParameters(rawUrl, config.Headers.RedactUrlParameters);
        string? source = context.Connection.RemoteIpAddress?.ToString();
        string? traceId = context.TraceIdentifier;
        int? userId = TryGetUserId(context.User, idEncryption);

        if (!RequestTraceHelper.MatchRequestStageFilters(config.Filters, source, method, rawUrl))
        {
            await next(context);
            return;
        }

        string requestHeaders = RequestTraceHelper.FormatHeaders(
            context.Request.Headers.Select(x => new KeyValuePair<string, IEnumerable<string>>(x.Key, x.Value.Select(v => v ?? string.Empty))),
            config.Headers.IncludeRequestHeaders,
            config.Headers.RedactRequestHeaders);

        RequestTraceRequestHeaderWriteModel requestHeaderModel = new()
        {
            LogId = logId,
            StartedAt = startedAt,
            ScheduledDeleteAt = RequestTraceHelper.ResolveScheduledDeleteAt(startedAt, config.RetentionDays),
            Direction = RequestTraceDirection.Inbound,
            Source = source,
            UserId = userId,
            TraceId = traceId,
            Method = method,
            Url = redactedUrl,
            RequestContentType = context.Request.ContentType,
            RequestHeaders = requestHeaders,
        };

        if (!queue.TryEnqueueRequestHeader(requestHeaderModel))
        {
            logger.LogWarning("Request trace queue dropped an inbound request-header event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
            await next(context);
            return;
        }

        int traceDeleted = 0;

        bool IsTraceDeleted() => Volatile.Read(ref traceDeleted) != 0;

        void TryDeleteTrace()
        {
            if (Interlocked.Exchange(ref traceDeleted, 1) != 0)
            {
                return;
            }

            if (!queue.TryEnqueueDelete(new RequestTraceDeleteWriteModel
            {
                LogId = logId,
            }))
            {
                logger.LogWarning("Request trace queue dropped an inbound delete event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                    logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
            }
        }

        bool captureRequestBody = config.Body.CaptureRequestBody || config.Body.CaptureRawRequestBody;
        if (captureRequestBody && context.Request.Body != Stream.Null && context.Request.Body.CanRead)
        {
            int rawCaptureLimit = RequestTraceHelper.ResolveRawCaptureLimit(null);
            Stream originalRequestBody = context.Request.Body;
            context.Request.Body = new ReadCaptureStream(
                originalRequestBody,
                rawCaptureLimit,
                (totalBytesRead, capturedBytes, _) =>
                {
                    try
                    {
                        if (IsTraceDeleted())
                        {
                            return;
                        }

                        (string? requestText, int? requestBodyLength) = config.Body.CaptureRequestBody
                            ? RequestTraceHelper.DecodeTextBody(
                                capturedBytes,
                                config.Body.MaxTextCharsForTruncate,
                                context.Request.Headers.ContentEncoding.ToString(),
                                config.Body.AllowedContentTypes,
                                context.Request.ContentType,
                                config.Body.RedactJsonFields)
                            : (null, null);

                        if (!queue.TryEnqueueRequestBody(new RequestTraceRequestBodyWriteModel
                        {
                            LogId = logId,
                            StartedAt = startedAt,
                            RequestBodyAt = DateTime.UtcNow,
                            Direction = RequestTraceDirection.Inbound,
                            Source = source,
                            UserId = userId,
                            TraceId = traceId,
                            Method = method,
                            Url = redactedUrl,
                            RequestContentType = context.Request.ContentType,
                            RawRequestBodyBytes = totalBytesRead,
                            RequestBodyLength = requestBodyLength ?? totalBytesRead,
                            RequestBody = requestText,
                            RequestBodyRaw = config.Body.CaptureRawRequestBody ? capturedBytes : null,
                        }))
                        {
                            logger.LogWarning("Request trace queue dropped an inbound request-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                                logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Inbound request-body trace callback failed and is ignored.");
                    }
                });
        }

        Stream originalResponseBody = context.Response.Body;
        WriteCaptureStream? tee = null;
        if (config.Body.CaptureResponseBody || config.Body.CaptureRawResponseBody)
        {
            tee = new WriteCaptureStream(originalResponseBody);
            context.Response.Body = tee;
        }

        Exception? pipelineException = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            pipelineException = ex;
        }
        finally
        {
            try
            {
                if (tee != null)
                {
                    context.Response.Body = originalResponseBody;
                }
            }
            catch (Exception traceEx)
            {
                logger.LogWarning(traceEx, "Inbound request trace post-processing failed and is ignored.");
            }
        }

        try
        {
            int durationMs = (int)Stopwatch.GetElapsedTime(startTick, Stopwatch.GetTimestamp()).TotalMilliseconds;
            short? statusCode = (short?)context.Response.StatusCode;
            bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, rawUrl, statusCode, durationMs);
            if (!shouldPersist)
            {
                TryDeleteTrace();
            }
            else if (pipelineException == null)
            {
                string? responseHeaders = RequestTraceHelper.FormatHeaders(
                    context.Response.Headers.Select(x => new KeyValuePair<string, IEnumerable<string>>(x.Key, x.Value.Select(v => v ?? string.Empty))),
                    config.Headers.IncludeResponseHeaders,
                    config.Headers.RedactResponseHeaders);

                RequestTraceResponseHeaderWriteModel responseHeaderModel = new()
                {
                    LogId = logId,
                    StartedAt = startedAt,
                    ResponseHeaderAt = DateTime.UtcNow,
                    Direction = RequestTraceDirection.Inbound,
                    Source = source,
                    UserId = userId,
                    TraceId = traceId,
                    Method = method,
                    Url = redactedUrl,
                    ResponseContentType = context.Response.ContentType,
                    StatusCode = statusCode,
                    ErrorType = null,
                    ErrorMessage = null,
                    ResponseHeaders = responseHeaders,
                };

                if (!queue.TryEnqueueResponseHeader(responseHeaderModel))
                {
                    logger.LogWarning("Request trace queue dropped an inbound response-header event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                        logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                }

                byte[]? responseBytes = tee?.CapturedBytes;
                (string? responseText, int? responseBodyLength) = config.Body.CaptureResponseBody
                    ? RequestTraceHelper.DecodeTextBody(
                        responseBytes,
                        config.Body.MaxTextCharsForTruncate,
                        context.Response.Headers.ContentEncoding.ToString(),
                        config.Body.AllowedContentTypes,
                        context.Response.ContentType,
                        config.Body.RedactJsonFields)
                    : (null, null);

                RequestTraceResponseBodyWriteModel responseBodyModel = new()
                {
                    LogId = logId,
                    StartedAt = startedAt,
                    ResponseBodyAt = DateTime.UtcNow,
                    Direction = RequestTraceDirection.Inbound,
                    Source = source,
                    UserId = userId,
                    TraceId = traceId,
                    Method = method,
                    Url = redactedUrl,
                    ResponseContentType = context.Response.ContentType,
                    StatusCode = statusCode,
                    RawResponseBodyBytes = responseBytes?.Length,
                    ResponseBodyLength = responseBodyLength ?? responseBytes?.Length ?? 0,
                    ResponseBody = responseText,
                    ResponseBodyRaw = config.Body.CaptureRawResponseBody ? responseBytes : null,
                };

                if (!queue.TryEnqueueResponseBody(responseBodyModel))
                {
                    logger.LogWarning("Request trace queue dropped an inbound response-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                        logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                }
            }
            else
            {
                RequestTraceExceptionWriteModel exceptionModel = new()
                {
                    LogId = logId,
                    StartedAt = startedAt,
                    ExceptionAt = DateTime.UtcNow,
                    Direction = RequestTraceDirection.Inbound,
                    Source = source,
                    UserId = userId,
                    TraceId = traceId,
                    Method = method,
                    Url = redactedUrl,
                    ResponseContentType = context.Response.ContentType,
                    StatusCode = statusCode,
                    ErrorType = pipelineException.GetType().Name,
                    ErrorMessage = pipelineException.ToString(),
                };

                if (!queue.TryEnqueueException(exceptionModel))
                {
                    logger.LogWarning("Request trace queue dropped an inbound exception event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                        logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                }
            }
        }
        catch (Exception traceEx)
        {
            logger.LogWarning(traceEx, "Inbound request trace post-processing failed and is ignored.");
        }

        if (pipelineException != null)
        {
            ExceptionDispatchInfo.Capture(pipelineException).Throw();
        }
    }

    private static int? TryGetUserId(ClaimsPrincipal user, IUrlEncryptionService idEncryption)
    {
        string? encryptedUserId = user.FindFirstValue(JwtPropertyKeys.UserId);
        return idEncryption.DecryptUserIdOrNull(encryptedUserId);
    }
}
