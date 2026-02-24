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

        byte[]? requestRaw = null;
        bool requestTruncated = false;
        if ((config.Body.CaptureRequestBody || config.Body.CaptureRawRequestBody) && request.Content != null)
        {
            (requestRaw, requestTruncated) = await TrySnapshotRequestBody(request, config.Body.MaxTextCharsForTruncate, cancellationToken);
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

        (string? requestText, bool requestTextTruncated) = config.Body.CaptureRequestBody
            ? RequestTraceHelper.DecodeTextBody(
                requestRaw,
                config.Body.MaxTextCharsForTruncate,
                request.Content?.Headers.ContentEncoding.FirstOrDefault(),
                config.Body.AllowedContentTypes,
                request.Content?.Headers.ContentType?.ToString())
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
            RequestContentType = request.Content?.Headers.ContentType?.ToString(),
            RawRequestBodyBytes = requestRaw?.Length ?? 0,
            IsRequestBodyTruncated = requestTruncated || requestTextTruncated,
            RequestBody = requestText,
            RequestBodyRaw = config.Body.CaptureRawRequestBody ? requestRaw : null,
        };

        if (!queue.TryEnqueueRequestBody(requestBodyModel))
        {
            logger.LogDebug("Request trace queue dropped an outbound request-body event. dropped={dropped}", queue.DroppedCount);
        }

        HttpResponseMessage? response = null;
        Exception? exception = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            try
            {
                int durationMs = (int)Stopwatch.GetElapsedTime(startTick, Stopwatch.GetTimestamp()).TotalMilliseconds;
                short? statusCode = (short?)response?.StatusCode;
                bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, url, statusCode, durationMs);
                if (shouldPersist)
                {
                    string? responseHeaders = response == null
                        ? null
                        : RequestTraceHelper.FormatHeaders(
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
                        ResponseContentType = response?.Content?.Headers.ContentType?.ToString(),
                        StatusCode = statusCode,
                        ErrorType = exception?.GetType().Name,
                        ErrorMessage = exception?.ToString(),
                        ResponseHeaders = responseHeaders,
                    };

                    if (!queue.TryEnqueueResponseHeader(responseHeaderModel))
                    {
                        logger.LogDebug("Request trace queue dropped an outbound response-header event. dropped={dropped}", queue.DroppedCount);
                    }

                    byte[]? responseRaw = null;
                    bool responseTruncated = false;
                    if (response != null && (config.Body.CaptureResponseBody || config.Body.CaptureRawResponseBody))
                    {
                        (responseRaw, responseTruncated) = await TrySnapshotResponseBody(response, config.Body.MaxTextCharsForTruncate, cancellationToken);
                    }

                    (string? responseText, bool responseTextTruncated) = config.Body.CaptureResponseBody
                        ? RequestTraceHelper.DecodeTextBody(
                            responseRaw,
                            config.Body.MaxTextCharsForTruncate,
                            response?.Content?.Headers.ContentEncoding.FirstOrDefault(),
                            config.Body.AllowedContentTypes,
                            response?.Content?.Headers.ContentType?.ToString())
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
                        ResponseContentType = response?.Content?.Headers.ContentType?.ToString(),
                        StatusCode = statusCode,
                        ErrorType = exception?.GetType().Name,
                        ErrorMessage = exception?.ToString(),
                        RawResponseBodyBytes = responseRaw?.Length,
                        IsResponseBodyTruncated = responseTruncated || responseTextTruncated,
                        ResponseBody = responseText,
                        ResponseBodyRaw = config.Body.CaptureRawResponseBody ? responseRaw : null,
                    };

                    if (!queue.TryEnqueueResponseBody(responseBodyModel))
                    {
                        logger.LogDebug("Request trace queue dropped an outbound response-body event. dropped={dropped}", queue.DroppedCount);
                    }
                }
            }
            catch (Exception traceEx)
            {
                logger.LogWarning(traceEx, "Outbound request trace post-processing failed and is ignored.");
            }
        }
    }

    private static async Task<(byte[]? bytes, bool truncated)> TrySnapshotRequestBody(HttpRequestMessage request, int maxBytes, CancellationToken cancellationToken)
    {
        HttpContent? content = request.Content;
        if (content == null)
        {
            return (null, false);
        }

        if (!RequestTraceHelper.IsSmallKnownLength(content.Headers.ContentLength, maxBytes))
        {
            return (null, true);
        }

        byte[] raw = await content.ReadAsByteArrayAsync(cancellationToken);
        ByteArrayContent clone = new(raw);
        CopyHeaders(content.Headers, clone.Headers);
        request.Content = clone;
        return (raw, false);
    }

    private static async Task<(byte[]? bytes, bool truncated)> TrySnapshotResponseBody(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        HttpContent? content = response.Content;
        if (content == null)
        {
            return (null, false);
        }

        string? contentType = content.Headers.ContentType?.ToString();
        if (RequestTraceHelper.IsLikelyStreamingContent(contentType))
        {
            return (null, false);
        }

        if (!RequestTraceHelper.IsSmallKnownLength(content.Headers.ContentLength, maxBytes))
        {
            return (null, true);
        }

        byte[] raw = await content.ReadAsByteArrayAsync(cancellationToken);
        ByteArrayContent clone = new(raw);
        CopyHeaders(content.Headers, clone.Headers);
        response.Content = clone;
        return (raw, false);
    }

    private static void CopyHeaders(HttpContentHeaders source, HttpContentHeaders target)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            target.TryAddWithoutValidation(header.Key, header.Value);
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
