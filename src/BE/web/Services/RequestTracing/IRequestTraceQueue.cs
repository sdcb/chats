using System.Threading.Channels;

namespace Chats.BE.Services.RequestTracing;

public interface IRequestTraceQueue
{
    bool TryEnqueueRequestHeader(RequestTraceRequestHeaderWriteModel item);

    bool TryEnqueueRequestBody(RequestTraceRequestBodyWriteModel item);

    bool TryEnqueueResponseHeader(RequestTraceResponseHeaderWriteModel item);

    bool TryEnqueueResponseBody(RequestTraceResponseBodyWriteModel item);

    IAsyncEnumerable<RequestTraceWriteModel> ReadAllAsync(CancellationToken cancellationToken);

    long DroppedCount { get; }
}

public sealed class RequestTraceQueue : IRequestTraceQueue
{
    private readonly Channel<RequestTraceWriteModel> _channel;
    private long _droppedCount;

    public RequestTraceQueue()
    {
        _channel = Channel.CreateBounded<RequestTraceWriteModel>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public bool TryEnqueueRequestHeader(RequestTraceRequestHeaderWriteModel item) => TryWrite(item);

    public bool TryEnqueueRequestBody(RequestTraceRequestBodyWriteModel item) => TryWrite(item);

    public bool TryEnqueueResponseHeader(RequestTraceResponseHeaderWriteModel item) => TryWrite(item);

    public bool TryEnqueueResponseBody(RequestTraceResponseBodyWriteModel item) => TryWrite(item);

    private bool TryWrite(RequestTraceWriteModel item)
    {
        bool written = _channel.Writer.TryWrite(item);
        if (!written)
        {
            Interlocked.Increment(ref _droppedCount);
        }

        return written;
    }

    public IAsyncEnumerable<RequestTraceWriteModel> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
