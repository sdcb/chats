using Chats.BE.Services.Options;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Chats.BE.Services.Configs;

public sealed class RequestTraceConfigProvider(
    IServiceScopeFactory scopeFactory,
    IOptions<RequestTraceSyncOptions> syncOptions,
    ILogger<RequestTraceConfigProvider> logger) : IRequestTraceConfigProvider
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _pollInterval = NormalizePollInterval(syncOptions.Value.PollInterval);

    private Snapshot _snapshot = Snapshot.Empty;

    public DateTime LastRefreshAtUtc => Volatile.Read(ref _snapshot).LastRefreshAtUtc;

    public RequestTraceConfig GetInboundConfig() => Volatile.Read(ref _snapshot).Inbound;

    public RequestTraceConfig GetOutboundConfig() => Volatile.Read(ref _snapshot).Outbound;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Snapshot current = Volatile.Read(ref _snapshot);
        DateTime now = DateTime.UtcNow;
        if (current.LastRefreshAtUtc != DateTime.MinValue && now - current.LastRefreshAtUtc < _pollInterval)
        {
            return;
        }

        await ForceRefreshAsync(cancellationToken);
    }

    public async Task ForceRefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

            Dictionary<string, string> values = await db.Configs
                .Where(x => x.Key == DBConfigKey.InboundRequestTrace || x.Key == DBConfigKey.OutboundRequestTrace)
                .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

            RequestTraceConfig inbound = ParseConfig(values, DBConfigKey.InboundRequestTrace);
            RequestTraceConfig outbound = ParseConfig(values, DBConfigKey.OutboundRequestTrace);

            Snapshot next = new(inbound, outbound, DateTime.UtcNow);
            Volatile.Write(ref _snapshot, next);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private RequestTraceConfig ParseConfig(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return new RequestTraceConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<RequestTraceConfig>(raw) ?? new RequestTraceConfig();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid request trace config for key {key}. Fallback to disabled default.", key);
            return new RequestTraceConfig();
        }
    }

    private static TimeSpan NormalizePollInterval(TimeSpan configured)
    {
        if (configured <= TimeSpan.Zero)
        {
            return TimeSpan.FromMinutes(30);
        }

        if (configured < TimeSpan.FromSeconds(30))
        {
            return TimeSpan.FromSeconds(30);
        }

        return configured;
    }

    private sealed record Snapshot(
        RequestTraceConfig Inbound,
        RequestTraceConfig Outbound,
        DateTime LastRefreshAtUtc)
    {
        public static readonly Snapshot Empty = new(new RequestTraceConfig(), new RequestTraceConfig(), DateTime.MinValue);
    }
}
