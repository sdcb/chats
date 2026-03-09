using Chats.BE.Services.Options;
using Chats.BE.Services.RequestTracing;
using Microsoft.Extensions.Options;

namespace Chats.BE.UnitTest.Services.RequestTracing;

public sealed class RequestTraceQueueTests
{
    [Fact]
    public async Task TryEnqueueDelete_ShouldFailImmediatelyWhenQueueIsFull()
    {
        RequestTraceQueue queue = new(Options.Create(new RequestTraceQueueOptions
        {
            Capacity = 2,
        }));

        Guid logId = Guid.CreateVersion7();
        DateTime startedAt = DateTime.UtcNow;

        Assert.True(queue.TryEnqueueRequestHeader(new RequestTraceRequestHeaderWriteModel
        {
            LogId = logId,
            StartedAt = startedAt,
            Direction = RequestTraceDirection.Inbound,
            Method = "GET",
            Url = "/v1/test",
            RequestHeaders = "x-a: 1",
        }));

        Assert.True(queue.TryEnqueueRequestBody(new RequestTraceRequestBodyWriteModel
        {
            LogId = logId,
            StartedAt = startedAt,
            Direction = RequestTraceDirection.Inbound,
            Method = "GET",
            Url = "/v1/test",
            RequestBodyAt = startedAt.AddSeconds(1),
            RawRequestBodyBytes = 1,
            RequestBodyLength = 1,
        }));

        Assert.False(queue.TryEnqueueDelete(new RequestTraceDeleteWriteModel
        {
            LogId = logId,
        }));

        Assert.False(queue.TryEnqueueResponseHeader(new RequestTraceResponseHeaderWriteModel
        {
            LogId = Guid.CreateVersion7(),
            StartedAt = startedAt,
            Direction = RequestTraceDirection.Inbound,
            Method = "GET",
            Url = "/v1/overflow",
            ResponseHeaderAt = startedAt.AddSeconds(2),
        }));

        await using IAsyncEnumerator<RequestTraceWriteModel> enumerator = queue.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        RequestTraceRequestHeaderWriteModel first = Assert.IsType<RequestTraceRequestHeaderWriteModel>(enumerator.Current);
        Assert.Equal(logId, first.LogId);

        Assert.True(await enumerator.MoveNextAsync());
        RequestTraceRequestBodyWriteModel second = Assert.IsType<RequestTraceRequestBodyWriteModel>(enumerator.Current);
        Assert.Equal(logId, second.LogId);

        Assert.Equal(0, queue.QueuedCount);
    }
}