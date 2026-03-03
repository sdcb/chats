using Chats.BE.Services.Configs;
using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
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
        string method = context.Request.Method;
        string url = context.Request.Path + context.Request.QueryString;
        string? source = context.Connection.RemoteIpAddress?.ToString();
        string? traceId = context.TraceIdentifier;
        int? userId = TryGetUserId(context.User, idEncryption);

        if (!RequestTraceHelper.MatchRequestStageFilters(config.Filters, source, method, url))
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
            Direction = RequestTraceDirection.Inbound,
            Source = source,
            UserId = userId,
            TraceId = traceId,
            Method = method,
            Url = url,
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

        bool captureRequestBody = config.Body.CaptureRequestBody || config.Body.CaptureRawRequestBody;
        if (captureRequestBody && context.Request.Body != Stream.Null && context.Request.Body.CanRead)
        {
            int rawCaptureLimit = RequestTraceHelper.ResolveRawCaptureLimit(null);
            Stream originalRequestBody = context.Request.Body;
            context.Request.Body = new ReadCaptureStream(
                originalRequestBody,
                rawCaptureLimit,
                (totalBytesRead, capturedBytes, truncated) =>
                {
                    try
                    {
                        (string? requestText, bool requestTextTruncated) = config.Body.CaptureRequestBody
                            ? RequestTraceHelper.DecodeTextBody(
                                capturedBytes,
                                config.Body.MaxTextCharsForTruncate,
                                context.Request.Headers.ContentEncoding.ToString(),
                                config.Body.AllowedContentTypes,
                                context.Request.ContentType)
                            : (null, false);

                        RequestTraceRequestBodyWriteModel requestBodyModel = new()
                        {
                            LogId = logId,
                            StartedAt = startedAt,
                            RequestBodyAt = DateTime.UtcNow,
                            Direction = RequestTraceDirection.Inbound,
                            Source = source,
                            UserId = userId,
                            TraceId = traceId,
                            Method = method,
                            Url = url,
                            RequestContentType = context.Request.ContentType,
                            RawRequestBodyBytes = totalBytesRead,
                            IsRequestBodyTruncated = truncated || requestTextTruncated,
                            RequestBody = requestText,
                            RequestBodyRaw = config.Body.CaptureRawRequestBody ? capturedBytes : null,
                        };

                        if (!queue.TryEnqueueRequestBody(requestBodyModel))
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

        context.Response.OnStarting(() =>
        {
            try
            {
                short? statusCode = (short?)context.Response.StatusCode;
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
                    Url = url,
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
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Inbound response-header trace callback failed and is ignored.");
            }

            return Task.CompletedTask;
        });

        context.Response.OnCompleted(() =>
        {
            try
            {
                byte[]? responseBytes = tee?.CapturedBytes;
                bool responseBodyTruncated = tee?.IsTruncated == true;
                short? statusCode = (short?)context.Response.StatusCode;

                (string? responseText, bool responseTextTruncated) = config.Body.CaptureResponseBody
                    ? RequestTraceHelper.DecodeTextBody(
                        responseBytes,
                        config.Body.MaxTextCharsForTruncate,
                        context.Response.Headers.ContentEncoding.ToString(),
                        config.Body.AllowedContentTypes,
                        context.Response.ContentType)
                    : (null, false);

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
                    Url = url,
                    ResponseContentType = context.Response.ContentType,
                    StatusCode = statusCode,
                    RawResponseBodyBytes = responseBytes?.Length,
                    IsResponseBodyTruncated = responseBodyTruncated || responseTextTruncated,
                    ResponseBody = responseText,
                    ResponseBodyRaw = config.Body.CaptureRawResponseBody ? responseBytes : null,
                };

                if (!queue.TryEnqueueResponseBody(responseBodyModel))
                {
                    logger.LogWarning("Request trace queue dropped an inbound response-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                        logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Inbound response-body trace callback failed and is ignored.");
            }

            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        catch (Exception ex)
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
                Url = url,
                ResponseContentType = context.Response.ContentType,
                StatusCode = (short?)context.Response.StatusCode,
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.ToString(),
            };

            if (!queue.TryEnqueueException(exceptionModel))
            {
                logger.LogWarning("Request trace queue dropped an inbound exception event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                    logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
            }

            throw;
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
    }

    private static int? TryGetUserId(ClaimsPrincipal user, IUrlEncryptionService idEncryption)
    {
        string? encryptedUserId = user.FindFirstValue(JwtPropertyKeys.UserId);
        return idEncryption.DecryptUserIdOrNull(encryptedUserId);
    }
}
