using Chats.BE.Services.RequestTracing;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.Services.RequestTracing;

public sealed class RequestTracePersistServiceTests
{
    private static ServiceProvider CreateServiceProvider(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PersistSingleAsync_InMemoryBranch_UpdatesByLogIdWithoutCandidateQuery()
    {
        ServiceProvider sp = CreateServiceProvider(nameof(PersistSingleAsync_InMemoryBranch_UpdatesByLogIdWithoutCandidateQuery));
        RequestTracePersistService service = new(
            new RequestTraceQueue(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RequestTracePersistService>.Instance);

        Guid logId = Guid.CreateVersion7();
        DateTime startedAt = DateTime.UtcNow;

        await service.PersistSingleAsync(new RequestTraceRequestHeaderWriteModel
        {
            LogId = logId,
            StartedAt = startedAt,
            Direction = RequestTraceDirection.Inbound,
            Source = "127.0.0.1",
            UserId = 1,
            TraceId = "trace-1",
            Method = "GET",
            Url = "/v1/test",
            RequestContentType = "application/json",
            RequestHeaders = "x-a: 1"
        }, CancellationToken.None);

        await service.PersistSingleAsync(new RequestTraceRequestBodyWriteModel
        {
            LogId = logId,
            StartedAt = startedAt.AddMinutes(1),
            Direction = RequestTraceDirection.Outbound,
            Source = "different-source",
            UserId = 2,
            TraceId = "trace-2",
            Method = "POST",
            Url = "/mismatch",
            RequestBodyAt = startedAt.AddSeconds(1),
            RequestContentType = "text/plain",
            RawRequestBodyBytes = 11,
            RequestBodyLength = 11,
            RequestBody = "hello world",
            RequestBodyRaw = [1, 2, 3]
        }, CancellationToken.None);

        await service.PersistSingleAsync(new RequestTraceResponseHeaderWriteModel
        {
            LogId = logId,
            StartedAt = startedAt.AddMinutes(2),
            Direction = RequestTraceDirection.Outbound,
            Source = "different-source-2",
            UserId = 3,
            TraceId = "trace-3",
            Method = "PATCH",
            Url = "/mismatch-2",
            ResponseHeaderAt = startedAt.AddSeconds(2),
            ResponseContentType = "application/json",
            StatusCode = 200,
            ResponseHeaders = "x-b: 2"
        }, CancellationToken.None);

        await service.PersistSingleAsync(new RequestTraceResponseBodyWriteModel
        {
            LogId = logId,
            StartedAt = startedAt.AddMinutes(3),
            Direction = RequestTraceDirection.Outbound,
            Source = "different-source-3",
            UserId = 4,
            TraceId = "trace-4",
            Method = "DELETE",
            Url = "/mismatch-3",
            ResponseBodyAt = startedAt.AddSeconds(3),
            ResponseContentType = "application/json",
            StatusCode = 201,
            RawResponseBodyBytes = 7,
            ResponseBodyLength = 4,
            ResponseBody = "resp",
            ResponseBodyRaw = [4, 5, 6]
        }, CancellationToken.None);

        await service.PersistSingleAsync(new RequestTraceExceptionWriteModel
        {
            LogId = logId,
            StartedAt = startedAt.AddMinutes(4),
            Direction = RequestTraceDirection.Outbound,
            Source = "different-source-4",
            UserId = 5,
            TraceId = "trace-5",
            Method = "PUT",
            Url = "/mismatch-4",
            ExceptionAt = startedAt.AddSeconds(4),
            ResponseContentType = "application/problem+json",
            StatusCode = 500,
            ErrorType = "TestException",
            ErrorMessage = "boom"
        }, CancellationToken.None);

        using IServiceScope scope = sp.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        RequestTrace trace = await db.RequestTraces.Include(x => x.RequestTracePayload).SingleAsync(x => x.Id == logId);

        Assert.Equal("GET", trace.Method);
        Assert.Equal("/v1/test", trace.Url);
        Assert.Equal("text/plain", trace.RequestContentType);
        Assert.Equal(11, trace.RawRequestBodyBytes);
        Assert.Equal(11, trace.RequestBodyLength);

        Assert.Equal("application/json", trace.ResponseContentType);
        Assert.Equal((short)201, trace.StatusCode);
        Assert.Equal(7, trace.RawResponseBodyBytes);
        Assert.Equal(4, trace.ResponseBodyLength);

        Assert.Equal("TestException", trace.ErrorType);

        Assert.NotNull(trace.RequestTracePayload);
        Assert.Equal("x-a: 1", trace.RequestTracePayload!.RequestHeaders);
        Assert.Equal("x-b: 2", trace.RequestTracePayload.ResponseHeaders);
        Assert.Equal("hello world", trace.RequestTracePayload.RequestBody);
        Assert.Equal("resp", trace.RequestTracePayload.ResponseBody);
        Assert.Equal("boom", trace.RequestTracePayload.ErrorMessage);
        Assert.Equal(new byte[] { 1, 2, 3 }, trace.RequestTracePayload.RequestBodyRaw);
        Assert.Equal(new byte[] { 4, 5, 6 }, trace.RequestTracePayload.ResponseBodyRaw);
    }
}
