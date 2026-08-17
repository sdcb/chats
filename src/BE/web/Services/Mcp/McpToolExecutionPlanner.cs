namespace Chats.BE.Services.Mcp;

public enum ToolExecutionKind
{
    Mcp,
    CodeInterpreter,
    Unknown,
}

public sealed record ToolExecutionPlanItem<T>(T Value, ToolExecutionKind Kind, bool ReadOnly);

public sealed record ToolExecutionBatch<T>(IReadOnlyList<ToolExecutionPlanItem<T>> Items)
{
    public bool IsParallel => Items.Count > 1;
}

public sealed class McpToolExecutionPlanner
{
    public const int MaxParallelism = 4;

    public IReadOnlyList<ToolExecutionBatch<T>> Plan<T>(IEnumerable<ToolExecutionPlanItem<T>> calls)
    {
        List<ToolExecutionBatch<T>> batches = [];
        List<ToolExecutionPlanItem<T>> readOnlyBatch = [];

        void FlushReadOnlyBatch()
        {
            if (readOnlyBatch.Count == 0) return;
            batches.Add(new ToolExecutionBatch<T>([.. readOnlyBatch]));
            readOnlyBatch.Clear();
        }

        foreach (ToolExecutionPlanItem<T> call in calls)
        {
            if (call.Kind == ToolExecutionKind.Mcp && call.ReadOnly)
            {
                readOnlyBatch.Add(call);
                if (readOnlyBatch.Count == MaxParallelism)
                {
                    FlushReadOnlyBatch();
                }
            }
            else
            {
                FlushReadOnlyBatch();
                batches.Add(new ToolExecutionBatch<T>([call]));
            }
        }

        FlushReadOnlyBatch();
        return batches;
    }
}
