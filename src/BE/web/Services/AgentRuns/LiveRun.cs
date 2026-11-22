namespace Chats.BE.Services.AgentRuns;

/// <summary>
/// In-memory state of a run executing on this instance. Events are buffered here (for local subscribers)
/// and persisted to <c>ChatRunEvent</c> by <see cref="AgentRunManager"/> for subscribers on other instances.
/// </summary>
public sealed class LiveRun
{
    private readonly object _lock = new();
    private readonly List<string> _events = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal LiveRun(long runId, int chatId, int userId)
    {
        RunId = runId;
        ChatId = chatId;
        UserId = userId;
    }

    public long RunId { get; }

    public int ChatId { get; }

    public int UserId { get; }

    public CancellationTokenSource Cts { get; } = new();

    internal CancellationTokenSource FlushStop { get; } = new();

    internal SemaphoreSlim FlushLock { get; } = new(1, 1);

    internal Task? FlushLoop { get; set; }

    internal int PersistedSeq { get; set; }

    internal DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;

    internal DateTime LastStopPollAt { get; set; } = DateTime.UtcNow;

    public bool IsCompleted { get; private set; }

    public int LastSeq
    {
        get
        {
            lock (_lock) return _events.Count;
        }
    }

    /// <summary>Appends a serialized event and returns its 1-based sequence number.</summary>
    public int Append(string json)
    {
        TaskCompletionSource toSignal;
        int seq;
        lock (_lock)
        {
            if (IsCompleted) throw new InvalidOperationException("Run already completed.");
            _events.Add(json);
            seq = _events.Count;
            toSignal = _changed;
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        toSignal.TrySetResult();
        return seq;
    }

    /// <summary>Returns events with seq &gt; <paramref name="after"/>, whether the run is completed, and a task signalled on next change.</summary>
    public (string[] Events, int FirstSeq, bool Completed, Task Changed) Read(int after)
    {
        lock (_lock)
        {
            int start = Math.Clamp(after, 0, _events.Count);
            string[] items = [.. _events.Skip(start)];
            return (items, start + 1, IsCompleted, _changed.Task);
        }
    }

    internal (int FirstSeq, string[] Events) PeekUnpersisted()
    {
        lock (_lock)
        {
            return (PersistedSeq + 1, [.. _events.Skip(PersistedSeq)]);
        }
    }

    internal void MarkCompleted()
    {
        TaskCompletionSource toSignal;
        lock (_lock)
        {
            IsCompleted = true;
            toSignal = _changed;
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        toSignal.TrySetResult();
    }
}
