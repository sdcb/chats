using System.Threading.Channels;
using Chats.BE.Services.Options;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.RequestTracing;

public interface IRequestTraceQueue
{
    bool TryEnqueueRequestHeader(RequestTraceRequestHeaderWriteModel item);

    bool TryEnqueueRequestBody(RequestTraceRequestBodyWriteModel item);

    bool TryEnqueueResponseHeader(RequestTraceResponseHeaderWriteModel item);

    bool TryEnqueueResponseBody(RequestTraceResponseBodyWriteModel item);

    bool TryEnqueueException(RequestTraceExceptionWriteModel item);

    IAsyncEnumerable<RequestTraceWriteModel> ReadAllAsync(CancellationToken cancellationToken);

    long DroppedCount { get; }

    long QueuedCount { get; }

    long QueueHighWatermark { get; }
}

public sealed class RequestTraceQueue : IRequestTraceQueue
{
    private readonly Channel<RequestTraceWriteModel> _channel;
    private long _droppedCount;
    private long _queueHighWatermark;

    public RequestTraceQueue()
        : this(new RequestTraceQueueOptions())
    {
    }

    public RequestTraceQueue(IOptions<RequestTraceQueueOptions> options)
        : this(options.Value)
    {
    }

    private RequestTraceQueue(RequestTraceQueueOptions options)
    {
        int capacity = options.Capacity > 0 ? options.Capacity : 1000;

        _channel = Channel.CreateBounded<RequestTraceWriteModel>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public long QueuedCount => _channel.Reader.CanCount ? _channel.Reader.Count : -1;

    public long QueueHighWatermark => Interlocked.Read(ref _queueHighWatermark);

    public bool TryEnqueueRequestHeader(RequestTraceRequestHeaderWriteModel item) => TryWrite(item);

    public bool TryEnqueueRequestBody(RequestTraceRequestBodyWriteModel item) => TryWrite(item);

    public bool TryEnqueueResponseHeader(RequestTraceResponseHeaderWriteModel item) => TryWrite(item);

    public bool TryEnqueueResponseBody(RequestTraceResponseBodyWriteModel item) => TryWrite(item);

    public bool TryEnqueueException(RequestTraceExceptionWriteModel item) => TryWrite(item);

    private bool TryWrite(RequestTraceWriteModel item)
    {
        bool written = _channel.Writer.TryWrite(item);
        if (!written)
        {
            Interlocked.Increment(ref _droppedCount);
            return false;
        }

        long currentQueued = QueuedCount;
        UpdateHighWatermark(currentQueued);
        return true;
    }

    public IAsyncEnumerable<RequestTraceWriteModel> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    private void UpdateHighWatermark(long currentQueued)
    {
        while (true)
        {
            long snapshot = Interlocked.Read(ref _queueHighWatermark);
            if (currentQueued <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _queueHighWatermark, currentQueued, snapshot) == snapshot)
            {
                return;
            }
        }
    }
}
