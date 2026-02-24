using Chats.BE.Services.Configs;
using Chats.BE.Services.RequestTracing;
using System.Diagnostics;
using System.Security.Claims;

namespace Chats.BE.Infrastructure;

public sealed class RequestTraceMiddleware(
    RequestDelegate next,
    IRequestTraceConfigProvider configProvider,
    IRequestTraceQueue queue,
    ILogger<RequestTraceMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        RequestTraceConfig config = configProvider.GetInboundConfig();
        if (!RequestTraceHelper.IsEnabledAndSampled(config))
        {
            await next(context);
            return;
        }

        DateTime startedAt = DateTime.UtcNow;
        long startTick = Stopwatch.GetTimestamp();
        string method = context.Request.Method;
        string url = context.Request.Path + context.Request.QueryString;
        string? source = context.Connection.RemoteIpAddress?.ToString();
        string? traceId = context.TraceIdentifier;
        int? userId = TryGetUserId(context.User);

        if (!RequestTraceHelper.MatchRequestStageFilters(config.Filters, source, method, url))
        {
            await next(context);
            return;
        }

        byte[]? requestBytes = null;
        bool requestBodyTruncated = false;

        bool captureRequestBody = config.Body.CaptureRequestBody || config.Body.CaptureRawRequestBody;
        if (captureRequestBody)
        {
            (requestBytes, requestBodyTruncated) = await ReadRequestBody(context.Request, config.Body.MaxTextCharsForTruncate, context.RequestAborted);
        }

        string requestHeaders = RequestTraceHelper.FormatHeaders(
            context.Request.Headers.Select(x => new KeyValuePair<string, IEnumerable<string>>(x.Key, x.Value.Select(v => v ?? string.Empty))),
            config.Headers.IncludeRequestHeaders,
            config.Headers.RedactRequestHeaders);

        (string? requestText, bool requestTextTruncated) = config.Body.CaptureRequestBody
            ? RequestTraceHelper.DecodeTextBody(
                requestBytes,
                config.Body.MaxTextCharsForTruncate,
                context.Request.Headers.ContentEncoding.ToString(),
                config.Body.AllowedContentTypes,
                context.Request.ContentType)
            : (null, false);

        RequestTraceRequestWriteModel requestModel = new()
        {
            StartedAt = startedAt,
            Direction = RequestTraceDirection.Inbound,
            Source = source,
            UserId = userId,
            TraceId = traceId,
            Method = method,
            Url = url,
            RequestContentType = context.Request.ContentType,
            RawRequestBodyBytes = requestBytes?.Length ?? 0,
            IsRequestBodyTruncated = requestBodyTruncated || requestTextTruncated,
            RequestHeaders = requestHeaders,
            RequestBody = requestText,
            RequestBodyRaw = config.Body.CaptureRawRequestBody ? requestBytes : null,
        };

        if (!queue.TryEnqueueRequest(requestModel))
        {
            logger.LogDebug("Request trace queue dropped an inbound request event. dropped={dropped}", queue.DroppedCount);
        }

        Stream originalResponseBody = context.Response.Body;
        TeeCaptureStream? tee = null;
        if (config.Body.CaptureResponseBody || config.Body.CaptureRawResponseBody)
        {
            tee = new TeeCaptureStream(originalResponseBody, config.Body.MaxTextCharsForTruncate);
            context.Response.Body = tee;
        }

        Exception? exception = null;
        try
        {
            await next(context);
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
                if (tee != null)
                {
                    context.Response.Body = originalResponseBody;
                }

                int durationMs = (int)Stopwatch.GetElapsedTime(startTick, Stopwatch.GetTimestamp()).TotalMilliseconds;
                short? statusCode = (short?)context.Response.StatusCode;

                bool shouldPersist = RequestTraceHelper.MatchResponseStageFilters(config.Filters, source, method, url, statusCode, durationMs);
                if (shouldPersist)
                {
                    string? responseHeaders = RequestTraceHelper.FormatHeaders(
                        context.Response.Headers.Select(x => new KeyValuePair<string, IEnumerable<string>>(x.Key, x.Value.Select(v => v ?? string.Empty))),
                        config.Headers.IncludeResponseHeaders,
                        config.Headers.RedactResponseHeaders);

                    byte[]? responseBytes = tee?.CapturedBytes;
                    bool responseBodyTruncated = tee?.IsTruncated == true;

                    (string? responseText, bool responseTextTruncated) = config.Body.CaptureResponseBody
                        ? RequestTraceHelper.DecodeTextBody(
                            responseBytes,
                            config.Body.MaxTextCharsForTruncate,
                            context.Response.Headers.ContentEncoding.ToString(),
                            config.Body.AllowedContentTypes,
                            context.Response.ContentType)
                        : (null, false);

                    RequestTraceResponseWriteModel responseModel = new()
                    {
                        StartedAt = startedAt,
                        DurationMs = durationMs,
                        Direction = RequestTraceDirection.Inbound,
                        Source = source,
                        UserId = userId,
                        TraceId = traceId,
                        Method = method,
                        Url = url,
                        ResponseContentType = context.Response.ContentType,
                        StatusCode = statusCode,
                        ErrorType = exception?.GetType().Name,
                        ErrorMessage = exception?.ToString(),
                        RawResponseBodyBytes = responseBytes?.Length,
                        IsResponseBodyTruncated = responseBodyTruncated || responseTextTruncated,
                        ResponseHeaders = responseHeaders,
                        ResponseBody = responseText,
                        ResponseBodyRaw = config.Body.CaptureRawResponseBody ? responseBytes : null,
                    };

                    if (!queue.TryEnqueueResponse(responseModel))
                    {
                        logger.LogDebug("Request trace queue dropped an inbound response event. dropped={dropped}", queue.DroppedCount);
                    }
                }
            }
            catch (Exception traceEx)
            {
                logger.LogWarning(traceEx, "Inbound request trace post-processing failed and is ignored.");
            }
        }
    }

    private static int? TryGetUserId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirstValue("UserId");
        if (int.TryParse(raw, out int value)) return value;
        return null;
    }

    private static async Task<(byte[]? bytes, bool truncated)> ReadRequestBody(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null || !request.Body.CanRead)
        {
            return (null, false);
        }

        request.EnableBuffering();
        int cap = Math.Max(0, maxBytes);
        if (cap == 0)
        {
            request.Body.Position = 0;
            return (null, false);
        }

        using MemoryStream output = new();
        byte[] buffer = new byte[8192];
        bool truncated = false;
        while (true)
        {
            int read = await request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            int remain = cap - (int)output.Length;
            if (remain <= 0)
            {
                truncated = true;
                continue;
            }

            int write = Math.Min(remain, read);
            await output.WriteAsync(buffer.AsMemory(0, write), cancellationToken);
            if (write < read)
            {
                truncated = true;
            }
        }

        request.Body.Position = 0;
        return (output.ToArray(), truncated);
    }
}
