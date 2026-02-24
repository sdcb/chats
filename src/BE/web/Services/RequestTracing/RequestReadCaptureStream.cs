namespace Chats.BE.Services.RequestTracing;

public sealed class RequestReadCaptureStream(
    Stream inner,
    int maxCaptureBytes,
    Action<int, byte[], bool> onCompleted) : Stream
{
    private readonly Stream _inner = inner;
    private readonly int _maxCaptureBytes = Math.Max(0, maxCaptureBytes);
    private readonly Action<int, byte[], bool> _onCompleted = onCompleted;
    private readonly MemoryStream _capture = new();
    private int _totalBytesRead;
    private bool _truncated;
    private int _completedFlag;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        AfterRead(read, buffer.AsSpan(offset, Math.Max(read, 0)));
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer);
        AfterRead(read, read > 0 ? buffer[..read] : default);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken);
        AfterRead(read, read > 0 ? buffer.Span[..read] : default);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        AfterRead(read, buffer.AsSpan(offset, Math.Max(read, 0)));
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CompleteOnce();
            _capture.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AfterRead(int read, ReadOnlySpan<byte> bytes)
    {
        if (read <= 0)
        {
            CompleteOnce();
            return;
        }

        _totalBytesRead += read;
        if (_truncated || _maxCaptureBytes == 0)
        {
            _truncated = _totalBytesRead > 0;
            return;
        }

        int remain = _maxCaptureBytes - (int)_capture.Length;
        if (remain <= 0)
        {
            _truncated = true;
            return;
        }

        int copyLength = Math.Min(remain, read);
        _capture.Write(bytes[..copyLength]);
        if (copyLength < read)
        {
            _truncated = true;
        }
    }

    private void CompleteOnce()
    {
        if (Interlocked.Exchange(ref _completedFlag, 1) != 0)
        {
            return;
        }

        _onCompleted(_totalBytesRead, _capture.ToArray(), _truncated);
    }
}
