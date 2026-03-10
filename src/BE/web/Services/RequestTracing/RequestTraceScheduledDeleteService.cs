using Chats.BE.Services.Options;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.RequestTracing;

public sealed class RequestTraceScheduledDeleteService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<RequestTraceCleanupOptions> cleanupOptions,
    ILogger<RequestTraceScheduledDeleteService> logger) : BackgroundService
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RequestTrace scheduled delete loop failed.");
            }

            try
            {
                await Task.Delay(LoopInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        if (!cleanupOptions.CurrentValue.Enabled)
        {
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        DateTime utcNow = DateTime.UtcNow;
        int deletedRows = await db.RequestTraces
            .Where(x => x.ScheduledDeleteAt != null && x.ScheduledDeleteAt <= utcNow)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedRows > 0)
        {
            logger.LogInformation("RequestTrace scheduled delete removed {count} rows.", deletedRows);
        }
    }
}