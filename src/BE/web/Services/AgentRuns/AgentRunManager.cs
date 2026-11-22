using Chats.DB;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Chats.BE.Services.AgentRuns;

/// <summary>
/// Tracks runs executing on this instance, persists their events/heartbeat to the database and
/// polls the database for stop requests so that stop/resume works across instances.
/// </summary>
public sealed class AgentRunManager(IServiceScopeFactory scopeFactory, ILogger<AgentRunManager> logger)
{
    public static string InstanceId { get; } = BuildInstanceId();

    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<long, LiveRun> _runs = new();

    public LiveRun Register(long runId, int chatId, int userId)
    {
        LiveRun run = new(runId, chatId, userId);
        if (!_runs.TryAdd(runId, run))
        {
            throw new InvalidOperationException($"Run {runId} is already registered.");
        }
        run.FlushLoop = Task.Run(() => FlushLoopAsync(run));
        return run;
    }

    public bool TryGet(long runId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LiveRun? run) => _runs.TryGetValue(runId, out run);

    public bool TryCancelLocal(long runId)
    {
        if (_runs.TryGetValue(runId, out LiveRun? run) && !run.IsCompleted && !run.Cts.IsCancellationRequested)
        {
            run.Cts.Cancel();
            return true;
        }
        return false;
    }

    public async Task CompleteAsync(LiveRun run, ChatRunStatus status)
    {
        run.FlushStop.Cancel();
        if (run.FlushLoop != null)
        {
            try { await run.FlushLoop; } catch (Exception ex) { logger.LogWarning(ex, "Run {RunId} flush loop faulted", run.RunId); }
        }

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await FlushAsync(run, status);
                break;
            }
            catch (Exception ex) when (attempt < 3)
            {
                logger.LogWarning(ex, "Final flush of run {RunId} failed, attempt {Attempt}", run.RunId, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Final flush of run {RunId} failed", run.RunId);
                break;
            }
        }

        run.MarkCompleted();
        _ = RemoveLaterAsync(run);
    }

    private async Task RemoveLaterAsync(LiveRun run)
    {
        await Task.Delay(CompletedRetention);
        if (_runs.TryRemove(run.RunId, out _))
        {
            run.Cts.Dispose();
            run.FlushStop.Dispose();
        }
    }

    private async Task FlushLoopAsync(LiveRun run)
    {
        using PeriodicTimer timer = new(FlushInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(run.FlushStop.Token))
            {
                try
                {
                    await FlushAsync(run, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Failed to flush run {RunId}", run.RunId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // completed
        }
    }

    private async Task FlushAsync(LiveRun run, ChatRunStatus? finalStatus)
    {
        await run.FlushLock.WaitAsync();
        try
        {
            (int firstSeq, string[] events) = run.PeekUnpersisted();
            DateTime now = DateTime.UtcNow;
            bool needHeartbeat = now - run.LastHeartbeatAt >= HeartbeatInterval;
            bool needStopPoll = finalStatus == null && now - run.LastStopPollAt >= StopPollInterval;
            if (events.Length == 0 && !needHeartbeat && !needStopPoll && finalStatus == null)
            {
                return;
            }

            using IServiceScope scope = scopeFactory.CreateScope();
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            if (events.Length > 0)
            {
                db.ChatRunEvents.Add(new ChatRunEvent
                {
                    RunId = run.RunId,
                    FirstSeq = firstSeq,
                    LastSeq = firstSeq + events.Length - 1,
                    Payload = "[" + string.Join(",", events) + "]",
                    CreatedAt = now,
                });
                await db.SaveChangesAsync(CancellationToken.None);
                run.PersistedSeq = firstSeq + events.Length - 1;
            }

            if (events.Length > 0 || needHeartbeat || finalStatus != null)
            {
                int lastSeq = run.PersistedSeq;
                if (finalStatus is ChatRunStatus status)
                {
                    await db.ChatRuns.Where(x => x.Id == run.RunId).ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.LastEventSeq, lastSeq)
                        .SetProperty(p => p.HeartbeatAt, now)
                        .SetProperty(p => p.Status, (byte)status)
                        .SetProperty(p => p.CompletedAt, now), CancellationToken.None);
                }
                else
                {
                    await db.ChatRuns.Where(x => x.Id == run.RunId).ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.LastEventSeq, lastSeq)
                        .SetProperty(p => p.HeartbeatAt, now), CancellationToken.None);
                }
                run.LastHeartbeatAt = now;
            }

            if (needStopPoll)
            {
                run.LastStopPollAt = now;
                bool stopRequested = await db.ChatRuns.AnyAsync(x => x.Id == run.RunId && x.StopRequestedAt != null, CancellationToken.None);
                if (stopRequested && !run.Cts.IsCancellationRequested)
                {
                    logger.LogInformation("Stop requested for run {RunId}", run.RunId);
                    run.Cts.Cancel();
                }
            }
        }
        finally
        {
            run.FlushLock.Release();
        }
    }

    /// <summary>Atomically claims queued steering messages of a run (Queued → Injected).</summary>
    public async Task<(long Id, string Content)[]> ClaimQueuedSteeringsAsync(long runId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        var queued = await db.ChatRunSteerings
            .Where(x => x.RunId == runId && x.Status == (byte)ChatRunSteeringStatus.Queued)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Content })
            .ToArrayAsync(cancellationToken);
        List<(long, string)> claimed = [];
        foreach (var item in queued)
        {
            DateTime now = DateTime.UtcNow;
            int affected = await db.ChatRunSteerings
                .Where(x => x.Id == item.Id && x.Status == (byte)ChatRunSteeringStatus.Queued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, (byte)ChatRunSteeringStatus.Injected)
                    .SetProperty(p => p.HandledAt, now), CancellationToken.None);
            if (affected == 1)
            {
                claimed.Add((item.Id, item.Content));
            }
        }
        return [.. claimed];
    }

    public async Task SetInjectedStepAsync(long[] steeringIds, long stepId)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        await db.ChatRunSteerings
            .Where(x => steeringIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.InjectedStepId, stepId), CancellationToken.None);
    }

    /// <summary>Marks all queued steering messages of a run as discarded and returns them.</summary>
    public async Task<SteeringDto[]> DiscardQueuedSteeringsAsync(long runId)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        return await DiscardQueuedSteeringsAsync(db, runId);
    }

    public static async Task<SteeringDto[]> DiscardQueuedSteeringsAsync(ChatsDB db, long runId)
    {
        ChatRunSteering[] queued = await db.ChatRunSteerings
            .Where(x => x.RunId == runId && x.Status == (byte)ChatRunSteeringStatus.Queued)
            .OrderBy(x => x.Id)
            .AsNoTracking()
            .ToArrayAsync(CancellationToken.None);
        List<SteeringDto> discarded = [];
        foreach (ChatRunSteering item in queued)
        {
            DateTime now = DateTime.UtcNow;
            int affected = await db.ChatRunSteerings
                .Where(x => x.Id == item.Id && x.Status == (byte)ChatRunSteeringStatus.Queued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, (byte)ChatRunSteeringStatus.Discarded)
                    .SetProperty(p => p.HandledAt, now), CancellationToken.None);
            if (affected == 1)
            {
                discarded.Add(new SteeringDto
                {
                    Id = item.Id,
                    Content = item.Content,
                    Status = ChatRunSteeringStatus.Discarded,
                    CreatedAt = item.CreatedAt,
                });
            }
        }
        return [.. discarded];
    }

    private static string BuildInstanceId()
    {
        string id = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        return id.Length <= 64 ? id : id[^64..];
    }
}
