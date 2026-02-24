namespace Chats.BE.Services.RequestTracing;

public sealed class TeeCaptureStream(Stream inner, int maxCaptureBytes) : Stream
{
    private readonly Stream _inner = inner;
    private readonly int _maxCaptureBytes = Math.Max(maxCaptureBytes, 0);
    private readonly MemoryStream _capture = new();
    private bool _truncated;

    public byte[] CapturedBytes => _capture.ToArray();

    public bool IsTruncated => _truncated;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        Capture(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        Capture(buffer);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken);
        Capture(buffer.Span);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        Capture(buffer.AsSpan(offset, count));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capture.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Capture(ReadOnlySpan<byte> bytes)
    {
        if (_truncated || _maxCaptureBytes == 0 || bytes.Length == 0)
        {
            return;
        }

        int remain = _maxCaptureBytes - (int)_capture.Length;
        if (remain <= 0)
        {
            _truncated = true;
            return;
        }

        int copyLength = Math.Min(remain, bytes.Length);
        _capture.Write(bytes[..copyLength]);
        if (copyLength < bytes.Length)
        {
            _truncated = true;
        }
    }
}
