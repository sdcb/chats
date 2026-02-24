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

        DateTime startedAt = DateTime.UtcNow;
        long startTick = Stopwatch.GetTimestamp();
        string method = request.Method.Method;
        string url = request.RequestUri?.ToString() ?? string.Empty;
        string? source = request.RequestUri?.Host;
        int? userId = TryGetUserId(httpContextAccessor.HttpContext?.User, idEncryption);

        if (!RequestTraceHelper.MatchRequestStageFilters(config.Filters, source, method, url))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        int rawCaptureLimit = RequestTraceHelper.ResolveRawCaptureLimit(null);

        bool captureRequestBody = config.Body.CaptureRequestBody || config.Body.CaptureRawRequestBody;
        string? requestContentType = request.Content?.Headers.ContentType?.ToString();
        string? requestContentEncoding = request.Content?.Headers.ContentEncoding.FirstOrDefault();
        if (captureRequestBody && request.Content != null)
        {
            HttpContent originalRequestContent = request.Content;
            request.Content = new ObservedRequestHttpContent(
                originalRequestContent,
                rawCaptureLimit,
                (rawBytesCount, capturedBytes, rawTruncated) =>
                {
                    try
                    {
                        (string? requestText, bool requestTextTruncated) = config.Body.CaptureRequestBody
                            ? RequestTraceHelper.DecodeTextBody(
                                capturedBytes,
                                config.Body.MaxTextCharsForTruncate,
                                requestContentEncoding,
                                config.Body.AllowedContentTypes,
                                requestContentType)
                            : (null, false);

                        RequestTraceRequestBodyWriteModel requestBodyModel = new()
                        {
                            StartedAt = startedAt,
                            RequestBodyAt = DateTime.UtcNow,
                            Direction = RequestTraceDirection.Outbound,
                            Source = source,
                            UserId = userId,
                            TraceId = null,
                            Method = method,
                            Url = url,
                            RequestContentType = requestContentType,
                            RawRequestBodyBytes = rawBytesCount,
                            IsRequestBodyTruncated = rawTruncated || requestTextTruncated,
                            RequestBody = requestText,
                            RequestBodyRaw = config.Body.CaptureRawRequestBody ? capturedBytes : null,
                        };

                        if (!queue.TryEnqueueRequestBody(requestBodyModel))
                        {
                            logger.LogDebug("Request trace queue dropped an outbound request-body event. dropped={dropped}", queue.DroppedCount);
                        }
                    }
                    catch (Exception traceEx)
                    {
                        logger.LogWarning(traceEx, "Outbound request-body trace callback failed and is ignored.");
                    }
                });
        }

        string requestHeaders = RequestTraceHelper.FormatHeaders(
            EnumerateHeaders(request.Headers, request.Content?.Headers),
            config.Headers.IncludeRequestHeaders,
            config.Headers.RedactRequestHeaders);

        RequestTraceRequestHeaderWriteModel requestHeaderModel = new()
        {
            StartedAt = startedAt,
            Direction = RequestTraceDirection.Outbound,
            Source = source,
            UserId = userId,
            TraceId = null,
            Method = method,
            Url = url,
            RequestContentType = request.Content?.Headers.ContentType?.ToString(),
            RequestHeaders = requestHeaders,
        };

        if (!queue.TryEnqueueRequestHeader(requestHeaderModel))
        {
            logger.LogDebug("Request trace queue dropped an outbound request-header event. dropped={dropped}", queue.DroppedCount);
        }

        if (captureRequestBody && request.Content == null)
        {
            RequestTraceRequestBodyWriteModel requestBodyModel = new()
            {
                StartedAt = startedAt,
                RequestBodyAt = DateTime.UtcNow,
                Direction = RequestTraceDirection.Outbound,
                Source = source,
                UserId = userId,
                TraceId = null,
                Method = method,
                Url = url,
                RequestContentType = null,
                RawRequestBodyBytes = 0,
                IsRequestBodyTruncated = false,
                RequestBody = null,
                RequestBodyRaw = null,
            };

            if (!queue.TryEnqueueRequestBody(requestBodyModel))
            {
                logger.LogDebug("Request trace queue dropped an outbound request-body event. dropped={dropped}", queue.DroppedCount);
            }
        }

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            int durationMs = (int)Stopwatch.GetElapsedTime(startTick, Stopwatch.GetTimestamp()).TotalMilliseconds;
            short? statusCode = (short?)response.StatusCode;
            bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, url, statusCode, durationMs);
            if (!shouldPersist)
            {
                return response;
            }

            string responseHeaders = RequestTraceHelper.FormatHeaders(
                EnumerateHeaders(response.Headers, response.Content?.Headers),
                config.Headers.IncludeResponseHeaders,
                config.Headers.RedactResponseHeaders);

            RequestTraceResponseHeaderWriteModel responseHeaderModel = new()
            {
                StartedAt = startedAt,
                ResponseHeaderAt = DateTime.UtcNow,
                Direction = RequestTraceDirection.Outbound,
                Source = source,
                UserId = userId,
                TraceId = null,
                Method = method,
                Url = url,
                ResponseContentType = response.Content?.Headers.ContentType?.ToString(),
                StatusCode = statusCode,
                ErrorType = null,
                ErrorMessage = null,
                ResponseHeaders = responseHeaders,
            };

            if (!queue.TryEnqueueResponseHeader(responseHeaderModel))
            {
                logger.LogDebug("Request trace queue dropped an outbound response-header event. dropped={dropped}", queue.DroppedCount);
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
                    (rawBytesCount, capturedBytes, rawTruncated) =>
                    {
                        try
                        {
                            (string? responseText, bool responseTextTruncated) = config.Body.CaptureResponseBody
                                ? RequestTraceHelper.DecodeTextBody(
                                    capturedBytes,
                                    config.Body.MaxTextCharsForTruncate,
                                    responseContentEncoding,
                                    config.Body.AllowedContentTypes,
                                    responseContentType)
                                : (null, false);

                            RequestTraceResponseBodyWriteModel responseBodyModel = new()
                            {
                                StartedAt = startedAt,
                                ResponseBodyAt = DateTime.UtcNow,
                                Direction = RequestTraceDirection.Outbound,
                                Source = source,
                                UserId = userId,
                                TraceId = null,
                                Method = method,
                                Url = url,
                                ResponseContentType = responseContentType,
                                StatusCode = statusCode,
                                RawResponseBodyBytes = rawBytesCount,
                                IsResponseBodyTruncated = rawTruncated || responseTextTruncated,
                                ResponseBody = responseText,
                                ResponseBodyRaw = config.Body.CaptureRawResponseBody ? capturedBytes : null,
                            };

                            if (!queue.TryEnqueueResponseBody(responseBodyModel))
                            {
                                logger.LogDebug("Request trace queue dropped an outbound response-body event. dropped={dropped}", queue.DroppedCount);
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
                    StartedAt = startedAt,
                    ResponseBodyAt = DateTime.UtcNow,
                    Direction = RequestTraceDirection.Outbound,
                    Source = source,
                    UserId = userId,
                    TraceId = null,
                    Method = method,
                    Url = url,
                    ResponseContentType = null,
                    StatusCode = statusCode,
                    RawResponseBodyBytes = 0,
                    IsResponseBodyTruncated = false,
                    ResponseBody = null,
                    ResponseBodyRaw = null,
                };

                if (!queue.TryEnqueueResponseBody(responseBodyModel))
                {
                    logger.LogDebug("Request trace queue dropped an outbound response-body event. dropped={dropped}", queue.DroppedCount);
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
                bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, url, statusCode, durationMs);
                if (shouldPersist)
                {
                    RequestTraceResponseHeaderWriteModel responseHeaderModel = new()
                    {
                        StartedAt = startedAt,
                        ResponseHeaderAt = DateTime.UtcNow,
                        Direction = RequestTraceDirection.Outbound,
                        Source = source,
                        UserId = userId,
                        TraceId = null,
                        Method = method,
                        Url = url,
                        ResponseContentType = null,
                        StatusCode = statusCode,
                        ErrorType = null,
                        ErrorMessage = null,
                        ResponseHeaders = null,
                    };

                    if (!queue.TryEnqueueResponseHeader(responseHeaderModel))
                    {
                        logger.LogDebug("Request trace queue dropped an outbound response-header event. dropped={dropped}", queue.DroppedCount);
                    }

                    bool captureResponseBody = config.Body.CaptureResponseBody || config.Body.CaptureRawResponseBody;
                    if (captureResponseBody)
                    {
                        RequestTraceResponseBodyWriteModel responseBodyModel = new()
                        {
                            StartedAt = startedAt,
                            ResponseBodyAt = DateTime.UtcNow,
                            Direction = RequestTraceDirection.Outbound,
                            Source = source,
                            UserId = userId,
                            TraceId = null,
                            Method = method,
                            Url = url,
                            ResponseContentType = null,
                            StatusCode = statusCode,
                            RawResponseBodyBytes = 0,
                            IsResponseBodyTruncated = false,
                            ResponseBody = null,
                            ResponseBodyRaw = null,
                        };

                        if (!queue.TryEnqueueResponseBody(responseBodyModel))
                        {
                            logger.LogDebug("Request trace queue dropped an outbound response-body event. dropped={dropped}", queue.DroppedCount);
                        }
                    }

                    RequestTraceExceptionWriteModel exceptionModel = new()
                    {
                        StartedAt = startedAt,
                        ExceptionAt = DateTime.UtcNow,
                        Direction = RequestTraceDirection.Outbound,
                        Source = source,
                        UserId = userId,
                        TraceId = null,
                        Method = method,
                        Url = url,
                        ResponseContentType = null,
                        StatusCode = statusCode,
                        ErrorType = ex.GetType().Name,
                        ErrorMessage = ex.ToString(),
                    };

                    if (!queue.TryEnqueueException(exceptionModel))
                    {
                        logger.LogDebug("Request trace queue dropped an outbound exception event. dropped={dropped}", queue.DroppedCount);
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
