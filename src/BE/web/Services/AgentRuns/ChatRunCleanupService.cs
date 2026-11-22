using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.AgentRuns;

/// <summary>
/// Marks orphaned runs (stale heartbeat) as failed, discards their queued steering messages,
/// and deletes persisted run events once they are no longer needed for resuming.
/// </summary>
public sealed class ChatRunCleanupService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ChatRunCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Chat run cleanup failed");
            }
        } while (await WaitNext(timer, stoppingToken));
    }

    private static async Task<bool> WaitNext(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        DateTime now = DateTime.UtcNow;
        DateTime staleBefore = now - AgentRunManager.HeartbeatTimeout;

        long[] staleRunIds = await db.ChatRuns
            .Where(x => x.Status == (byte)ChatRunStatus.Running && x.HeartbeatAt < staleBefore)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        foreach (long runId in staleRunIds)
        {
            int affected = await db.ChatRuns
                .Where(x => x.Id == runId && x.Status == (byte)ChatRunStatus.Running && x.HeartbeatAt < staleBefore)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, (byte)ChatRunStatus.Failed)
                    .SetProperty(p => p.CompletedAt, now), cancellationToken);
            if (affected == 1)
            {
                logger.LogWarning("Run {RunId} marked as failed because its heartbeat timed out", runId);
            }
        }

        // Late steering messages that arrived after a run had finished are never injected.
        await db.ChatRunSteerings
            .Where(x => x.Status == (byte)ChatRunSteeringStatus.Queued && x.Run.Status != (byte)ChatRunStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, (byte)ChatRunSteeringStatus.Discarded)
                .SetProperty(p => p.HandledAt, now), cancellationToken);

        int retentionMinutes = Math.Max(1, configuration.GetValue("ChatRun:EventRetentionMinutes", 60));
        DateTime completedBefore = now.AddMinutes(-retentionMinutes);
        int deleted = await db.ChatRunEvents
            .Where(x => x.Run.Status != (byte)ChatRunStatus.Running && x.Run.CompletedAt < completedBefore)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} expired chat run event batches", deleted);
        }
    }
}
