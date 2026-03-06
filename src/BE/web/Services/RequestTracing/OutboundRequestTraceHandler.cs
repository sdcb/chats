using Chats.BE.Services.Configs;
using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Chats.BE.Services.RequestTracing;

public sealed class OutboundRequestTraceHandler(
    IRequestTraceConfigProvider configProvider,
    IRequestTraceQueue queue,
    IHttpContextAccessor httpContextAccessor,
    IUrlEncryptionService idEncryption,
    ILogger<OutboundRequestTraceHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestTraceConfig config = configProvider.GetOutboundConfig();
        if (!RequestTraceHelper.IsEnabledAndSampled(config))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        Guid logId = Guid.CreateVersion7();
        DateTime startedAt = DateTime.UtcNow;
        long startTick = Stopwatch.GetTimestamp();
        string method = request.Method.Method;
        string rawUrl = request.RequestUri?.ToString() ?? string.Empty;
        string redactedUrl = RequestTraceHelper.RedactUrlQueryParameters(rawUrl, config.Headers.RedactUrlParameters);
        string source = HttpClientTracingContext.GetClientName(request);
        int? userId = TryGetUserId(httpContextAccessor.HttpContext?.User, idEncryption);
        string? traceId = httpContextAccessor.HttpContext?.TraceIdentifier;

        if (!RequestTraceHelper.MatchRequestStageFilters(config.Filters, source, method, rawUrl))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        string? requestContentType = request.Content?.Headers.ContentType?.ToString();

        string requestHeaders = RequestTraceHelper.FormatHeaders(
            EnumerateHeaders(request.Headers, request.Content?.Headers),
            config.Headers.IncludeRequestHeaders,
            config.Headers.RedactRequestHeaders);

        RequestTraceRequestHeaderWriteModel requestHeaderModel = new()
        {
            LogId = logId,
            StartedAt = startedAt,
            ScheduledDeleteAt = RequestTraceHelper.ResolveScheduledDeleteAt(startedAt, config.RetentionDays),
            Direction = RequestTraceDirection.Outbound,
            Source = source,
            UserId = userId,
            TraceId = traceId,
            Method = method,
            Url = redactedUrl,
            RequestContentType = request.Content?.Headers.ContentType?.ToString(),
            RequestHeaders = requestHeaders,
        };

        RequestTraceRequestBodyWriteModel? requestBodyModel = null;

        int rawCaptureLimit = RequestTraceHelper.ResolveRawCaptureLimit(null);

        bool captureRequestBody = config.Body.CaptureRequestBody || config.Body.CaptureRawRequestBody;
        string? requestContentEncoding = request.Content?.Headers.ContentEncoding.FirstOrDefault();
        if (captureRequestBody && request.Content != null)
        {
            HttpContent originalRequestContent = request.Content;
            request.Content = new ObservedRequestHttpContent(
                originalRequestContent,
                rawCaptureLimit,
                (rawBytesCount, capturedBytes, _) =>
                {
                    try
                    {
                        (string? requestText, int? requestBodyLength) = config.Body.CaptureRequestBody
                            ? RequestTraceHelper.DecodeTextBody(
                                capturedBytes,
                                config.Body.MaxTextCharsForTruncate,
                                requestContentEncoding,
                                config.Body.AllowedContentTypes,
                                requestContentType,
                                config.Body.RedactJsonFields)
                            : (null, null);

                        RequestTraceRequestBodyWriteModel capturedRequestBodyModel = new()
                        {
                            LogId = logId,
                            StartedAt = startedAt,
                            RequestBodyAt = DateTime.UtcNow,
                            Direction = RequestTraceDirection.Outbound,
                            Source = source,
                            UserId = userId,
                            TraceId = traceId,
                            Method = method,
                            Url = redactedUrl,
                            RequestContentType = requestContentType,
                            RawRequestBodyBytes = rawBytesCount,
                            RequestBodyLength = requestBodyLength ?? rawBytesCount,
                            RequestBody = requestText,
                            RequestBodyRaw = config.Body.CaptureRawRequestBody ? capturedBytes : null,
                        };

                        requestBodyModel = capturedRequestBodyModel;
                    }
                    catch (Exception traceEx)
                    {
                        logger.LogWarning(traceEx, "Outbound request-body trace callback failed and is ignored.");
                    }
                });
        }

        if (captureRequestBody && request.Content == null)
        {
            requestBodyModel = new RequestTraceRequestBodyWriteModel
            {
                LogId = logId,
                StartedAt = startedAt,
                RequestBodyAt = DateTime.UtcNow,
                Direction = RequestTraceDirection.Outbound,
                Source = source,
                UserId = userId,
                TraceId = traceId,
                Method = method,
                Url = redactedUrl,
                RequestContentType = null,
                RawRequestBodyBytes = 0,
                RequestBodyLength = 0,
                RequestBody = null,
                RequestBodyRaw = null,
            };
        }

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            int durationMs = (int)Stopwatch.GetElapsedTime(startTick, Stopwatch.GetTimestamp()).TotalMilliseconds;
            short? statusCode = (short?)response.StatusCode;
            bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, rawUrl, statusCode, durationMs);
            if (!shouldPersist)
            {
                return response;
            }

            if (!queue.TryEnqueueRequestHeader(requestHeaderModel))
            {
                logger.LogWarning("Request trace queue dropped an outbound request-header event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                    logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);

                return response;
            }

            if (requestBodyModel != null && !queue.TryEnqueueRequestBody(requestBodyModel))
            {
                logger.LogWarning("Request trace queue dropped an outbound request-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                    logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
            }

            string responseHeaders = RequestTraceHelper.FormatHeaders(
                EnumerateHeaders(response.Headers, response.Content?.Headers),
                config.Headers.IncludeResponseHeaders,
                config.Headers.RedactResponseHeaders);

            RequestTraceResponseHeaderWriteModel responseHeaderModel = new()
            {
                LogId = logId,
                StartedAt = startedAt,
                ResponseHeaderAt = DateTime.UtcNow,
                Direction = RequestTraceDirection.Outbound,
                Source = source,
                UserId = userId,
                TraceId = traceId,
                Method = method,
                Url = redactedUrl,
                ResponseContentType = response.Content?.Headers.ContentType?.ToString(),
                StatusCode = statusCode,
                ErrorType = null,
                ErrorMessage = null,
                ResponseHeaders = responseHeaders,
            };

            if (!queue.TryEnqueueResponseHeader(responseHeaderModel))
            {
                logger.LogWarning("Request trace queue dropped an outbound response-header event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                    logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
            }

            bool captureResponseBody = config.Body.CaptureResponseBody || config.Body.CaptureRawResponseBody;
            if (captureResponseBody && response.Content != null)
            {
                string? responseContentType = response.Content.Headers.ContentType?.ToString();
                string? responseContentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
                HttpContent originalResponseContent = response.Content;
                response.Content = new ObservedResponseHttpContent(
                    originalResponseContent,
                    rawCaptureLimit,
                    (rawBytesCount, capturedBytes, _) =>
                    {
                        try
                        {
                            (string? responseText, int? responseBodyLength) = config.Body.CaptureResponseBody
                                ? RequestTraceHelper.DecodeTextBody(
                                    capturedBytes,
                                    config.Body.MaxTextCharsForTruncate,
                                    responseContentEncoding,
                                    config.Body.AllowedContentTypes,
                                    responseContentType,
                                    config.Body.RedactJsonFields)
                                : (null, null);

                            RequestTraceResponseBodyWriteModel responseBodyModel = new()
                            {
                                LogId = logId,
                                StartedAt = startedAt,
                                ResponseBodyAt = DateTime.UtcNow,
                                Direction = RequestTraceDirection.Outbound,
                                Source = source,
                                UserId = userId,
                                TraceId = traceId,
                                Method = method,
                                Url = redactedUrl,
                                ResponseContentType = responseContentType,
                                StatusCode = statusCode,
                                RawResponseBodyBytes = rawBytesCount,
                                ResponseBodyLength = responseBodyLength ?? rawBytesCount,
                                ResponseBody = responseText,
                                ResponseBodyRaw = config.Body.CaptureRawResponseBody ? capturedBytes : null,
                            };

                            if (!queue.TryEnqueueResponseBody(responseBodyModel))
                            {
                                logger.LogWarning("Request trace queue dropped an outbound response-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                                    logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                            }
                        }
                        catch (Exception traceEx)
                        {
                            logger.LogWarning(traceEx, "Outbound response-body trace callback failed and is ignored.");
                        }
                    });
            }
            else if (captureResponseBody)
            {
                RequestTraceResponseBodyWriteModel responseBodyModel = new()
                {
                    LogId = logId,
                    StartedAt = startedAt,
                    ResponseBodyAt = DateTime.UtcNow,
                    Direction = RequestTraceDirection.Outbound,
                    Source = source,
                    UserId = userId,
                    TraceId = traceId,
                    Method = method,
                    Url = redactedUrl,
                    ResponseContentType = null,
                    StatusCode = statusCode,
                    RawResponseBodyBytes = 0,
                    ResponseBodyLength = 0,
                    ResponseBody = null,
                    ResponseBodyRaw = null,
                };

                if (!queue.TryEnqueueResponseBody(responseBodyModel))
                {
                    logger.LogWarning("Request trace queue dropped an outbound response-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                        logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            try
            {
                int durationMs = (int)Stopwatch.GetElapsedTime(startTick, Stopwatch.GetTimestamp()).TotalMilliseconds;
                short? statusCode = null;
                bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, rawUrl, statusCode, durationMs);
                if (shouldPersist)
                {
                    if (!queue.TryEnqueueRequestHeader(requestHeaderModel))
                    {
                        logger.LogWarning("Request trace queue dropped an outbound request-header event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                            logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);

                        throw;
                    }

                    if (requestBodyModel != null && !queue.TryEnqueueRequestBody(requestBodyModel))
                    {
                        logger.LogWarning("Request trace queue dropped an outbound request-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                            logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                    }

                    RequestTraceResponseHeaderWriteModel responseHeaderModel = new()
                    {
                        LogId = logId,
                        StartedAt = startedAt,
                        ResponseHeaderAt = DateTime.UtcNow,
                        Direction = RequestTraceDirection.Outbound,
                        Source = source,
                        UserId = userId,
                        TraceId = traceId,
                        Method = method,
                        Url = redactedUrl,
                        ResponseContentType = null,
                        StatusCode = statusCode,
                        ErrorType = null,
                        ErrorMessage = null,
                        ResponseHeaders = null,
                    };

                    if (!queue.TryEnqueueResponseHeader(responseHeaderModel))
                    {
                        logger.LogWarning("Request trace queue dropped an outbound response-header event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                            logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                    }

                    bool captureResponseBody = config.Body.CaptureResponseBody || config.Body.CaptureRawResponseBody;
                    if (captureResponseBody)
                    {
                        RequestTraceResponseBodyWriteModel responseBodyModel = new()
                        {
                            LogId = logId,
                            StartedAt = startedAt,
                            ResponseBodyAt = DateTime.UtcNow,
                            Direction = RequestTraceDirection.Outbound,
                            Source = source,
                            UserId = userId,
                            TraceId = traceId,
                            Method = method,
                            Url = redactedUrl,
                            ResponseContentType = null,
                            StatusCode = statusCode,
                            RawResponseBodyBytes = null,
                            ResponseBodyLength = null,
                            ResponseBody = null,
                            ResponseBodyRaw = null,
                        };

                        if (!queue.TryEnqueueResponseBody(responseBodyModel))
                        {
                            logger.LogWarning("Request trace queue dropped an outbound response-body event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                                logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                        }
                    }

                    RequestTraceExceptionWriteModel exceptionModel = new()
                    {
                        LogId = logId,
                        StartedAt = startedAt,
                        ExceptionAt = DateTime.UtcNow,
                        Direction = RequestTraceDirection.Outbound,
                        Source = source,
                        UserId = userId,
                        TraceId = traceId,
                        Method = method,
                        Url = redactedUrl,
                        ResponseContentType = null,
                        StatusCode = statusCode,
                        ErrorType = ex.GetType().Name,
                        ErrorMessage = ex.ToString(),
                    };

                    if (!queue.TryEnqueueException(exceptionModel))
                    {
                        logger.LogWarning("Request trace queue dropped an outbound exception event. logId={logId}, traceId={traceId}, dropped={dropped}, queued={queued}, highWatermark={highWatermark}",
                            logId, traceId, queue.DroppedCount, queue.QueuedCount, queue.QueueHighWatermark);
                    }
                }
            }
            catch (Exception traceEx)
            {
                logger.LogWarning(traceEx, "Outbound request trace post-processing failed and is ignored.");
            }

            throw;
        }
    }

    private static KeyValuePair<string, IEnumerable<string>> ToHeaderItem(KeyValuePair<string, IEnumerable<string>> item)
        => new(item.Key, item.Value);

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> EnumerateHeaders(HttpResponseHeaders baseHeaders, HttpContentHeaders? contentHeaders)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in baseHeaders)
        {
            yield return ToHeaderItem(header);
        }

        if (contentHeaders != null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in contentHeaders)
            {
                yield return ToHeaderItem(header);
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> EnumerateHeaders(HttpRequestHeaders baseHeaders, HttpContentHeaders? contentHeaders)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in baseHeaders)
        {
            yield return ToHeaderItem(header);
        }

        if (contentHeaders != null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in contentHeaders)
            {
                yield return ToHeaderItem(header);
            }
        }
    }

    private static int? TryGetUserId(ClaimsPrincipal? user, IUrlEncryptionService idEncryption)
    {
        if (user == null)
        {
            return null;
        }

        string? encryptedUserId = user.FindFirstValue(JwtPropertyKeys.UserId);
        return idEncryption.DecryptUserIdOrNull(encryptedUserId);
    }
}
