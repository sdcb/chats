using System.Net;
using System.Net.Http.Headers;

namespace Chats.BE.Services.RequestTracing;

internal sealed class ObservedRequestHttpContent : HttpContent
{
    private readonly HttpContent _inner;
    private readonly int _maxCaptureBytes;
    private readonly Action<int, byte[], bool> _onCompleted;
    private int _completedFlag;

    public ObservedRequestHttpContent(HttpContent inner, int maxCaptureBytes, Action<int, byte[], bool> onCompleted)
    {
        _inner = inner;
        _maxCaptureBytes = maxCaptureBytes;
        _onCompleted = onCompleted;
        CopyHeaders(_inner.Headers, Headers);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using WriteCaptureStream captureStream = new(stream, _maxCaptureBytes);
        try
        {
            await _inner.CopyToAsync(captureStream, context, cancellationToken);
            CompleteOnce(captureStream.TotalBytesWritten, captureStream.CapturedBytes, captureStream.IsTruncated);
        }
        catch
        {
            CompleteOnce(captureStream.TotalBytesWritten, captureStream.CapturedBytes, true);
            throw;
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        if (_inner.Headers.ContentLength.HasValue)
        {
            length = _inner.Headers.ContentLength.Value;
            return true;
        }

        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void CompleteOnce(int totalBytes, byte[] capturedBytes, bool truncated)
    {
        if (Interlocked.Exchange(ref _completedFlag, 1) != 0)
        {
            return;
        }

        _onCompleted(totalBytes, capturedBytes, truncated);
    }

    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        return _inner.ReadAsStreamAsync();
    }

    protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
    {
        return _inner.ReadAsStream(cancellationToken);
    }

    private static void CopyHeaders(HttpContentHeaders source, HttpContentHeaders target)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            target.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}

internal sealed class ObservedResponseHttpContent : HttpContent
{
    private readonly HttpContent _inner;
    private readonly int _maxCaptureBytes;
    private readonly Action<int, byte[], bool> _onCompleted;
    private int _completedFlag;

    public ObservedResponseHttpContent(HttpContent inner, int maxCaptureBytes, Action<int, byte[], bool> onCompleted)
    {
        _inner = inner;
        _maxCaptureBytes = maxCaptureBytes;
        _onCompleted = onCompleted;
        CopyHeaders(_inner.Headers, Headers);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await using Stream source = await _inner.ReadAsStreamAsync(cancellationToken);
        using ReadCaptureStream captureStream = new(
            source,
            _maxCaptureBytes,
            CompleteOnce);

        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = await captureStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        if (_inner.Headers.ContentLength.HasValue)
        {
            length = _inner.Headers.ContentLength.Value;
            return true;
        }

        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override async Task<Stream> CreateContentReadStreamAsync()
    {
        Stream source = await _inner.ReadAsStreamAsync();
        return new ReadCaptureStream(source, _maxCaptureBytes, CompleteOnce);
    }

    protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
    {
        Stream source = _inner.ReadAsStream(cancellationToken);
        return new ReadCaptureStream(source, _maxCaptureBytes, CompleteOnce);
    }

    private void CompleteOnce(int totalBytes, byte[] capturedBytes, bool truncated)
    {
        if (Interlocked.Exchange(ref _completedFlag, 1) != 0)
        {
            return;
        }

        _onCompleted(totalBytes, capturedBytes, truncated);
    }


    private static void CopyHeaders(HttpContentHeaders source, HttpContentHeaders target)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            target.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
