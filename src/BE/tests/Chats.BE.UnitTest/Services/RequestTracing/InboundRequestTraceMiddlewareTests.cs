using Chats.BE.Services.Configs;
using Chats.BE.Services.RequestTracing;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace Chats.BE.UnitTest.Services.RequestTracing;

public sealed class InboundRequestTraceMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenPipelineThrows_RecordsFinalInternalServerErrorStatus()
    {
        CapturingRequestTraceQueue queue = new();
        RequestTraceConfig config = new()
        {
            Enabled = true,
            Filters = new RequestTraceFilters
            {
                Include = new RequestTraceFilterRuleSet
                {
                    StatusCodes = ["5xx"],
                },
            },
        };
        InboundRequestTraceMiddleware middleware = new(
            _ => throw new InvalidOperationException("boom"),
            new StaticRequestTraceConfigProvider(config),
            queue,
            new NoOpUrlEncryptionService(),
            NullLogger<InboundRequestTraceMiddleware>.Instance);
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/trace-failure";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.Invoke(context));

        RequestTraceExceptionWriteModel exception = Assert.IsType<RequestTraceExceptionWriteModel>(Assert.Single(queue.Items.Skip(1)));
        Assert.Equal((short)StatusCodes.Status500InternalServerError, exception.StatusCode);
    }

    private sealed class StaticRequestTraceConfigProvider(RequestTraceConfig config) : IRequestTraceConfigProvider
    {
        public DateTime LastRefreshAtUtc => DateTime.UtcNow;

        public RequestTraceConfig GetInboundConfig() => config;

        public RequestTraceConfig GetOutboundConfig() => config;

        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ForceRefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingRequestTraceQueue : IRequestTraceQueue
    {
        public List<RequestTraceWriteModel> Items { get; } = [];

        public long DroppedCount => 0;

        public long QueuedCount => Items.Count;

        public long QueueHighWatermark => Items.Count;

        public bool TryEnqueueRequestHeader(RequestTraceRequestHeaderWriteModel item) => Add(item);

        public bool TryEnqueueRequestBody(RequestTraceRequestBodyWriteModel item) => Add(item);

        public bool TryEnqueueResponseHeader(RequestTraceResponseHeaderWriteModel item) => Add(item);

        public bool TryEnqueueResponseBody(RequestTraceResponseBodyWriteModel item) => Add(item);

        public bool TryEnqueueException(RequestTraceExceptionWriteModel item) => Add(item);

        public bool TryEnqueueDelete(RequestTraceDeleteWriteModel item) => Add(item);

        public async IAsyncEnumerable<RequestTraceWriteModel> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        private bool Add(RequestTraceWriteModel item)
        {
            Items.Add(item);
            return true;
        }
    }
}
