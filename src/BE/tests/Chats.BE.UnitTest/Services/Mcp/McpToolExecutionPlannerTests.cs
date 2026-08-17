using Chats.BE.Services.Mcp;

namespace Chats.BE.UnitTest.Services.Mcp;

public sealed class McpToolExecutionPlannerTests
{
    private readonly McpToolExecutionPlanner planner = new();

    [Fact]
    public void Plan_MixedCalls_UsesReadOnlyRunsBetweenSerialBarriers()
    {
        IReadOnlyList<ToolExecutionBatch<int>> batches = Plan(
            Mcp(readOnly: false),
            Mcp(readOnly: true),
            Mcp(readOnly: true),
            Mcp(readOnly: false),
            Mcp(readOnly: false));

        AssertBatches(batches, [1], [2, 3], [4], [5]);
    }

    [Fact]
    public void Plan_AllReadOnly_SplitsAtMaximumParallelism()
    {
        IReadOnlyList<ToolExecutionBatch<int>> batches = Plan(
            Mcp(true), Mcp(true), Mcp(true), Mcp(true), Mcp(true), Mcp(true));

        AssertBatches(batches, [1, 2, 3, 4], [5, 6]);
        Assert.All(batches, batch => Assert.InRange(batch.Items.Count, 1, McpToolExecutionPlanner.MaxParallelism));
    }

    [Fact]
    public void Plan_AllNonReadOnly_UsesSingletonBatches()
    {
        IReadOnlyList<ToolExecutionBatch<int>> batches = Plan(Mcp(false), Mcp(false), Mcp(false));

        AssertBatches(batches, [1], [2], [3]);
    }

    [Fact]
    public void Plan_CodeInterpreterAndUnknown_AreSerialBarriers()
    {
        IReadOnlyList<ToolExecutionBatch<int>> batches = Plan(
            Mcp(true),
            new(ToolExecutionKind.CodeInterpreter, false),
            Mcp(true),
            new(ToolExecutionKind.Unknown, false),
            Mcp(true));

        AssertBatches(batches, [1], [2], [3], [4], [5]);
    }

    [Fact]
    public void Plan_EmptyInput_ReturnsNoBatches()
    {
        Assert.Empty(planner.Plan(Array.Empty<ToolExecutionPlanItem<int>>()));
    }

    private IReadOnlyList<ToolExecutionBatch<int>> Plan(params Input[] inputs)
        => planner.Plan(inputs.Select((input, index) =>
            new ToolExecutionPlanItem<int>(index + 1, input.Kind, input.ReadOnly)));

    private static Input Mcp(bool readOnly) => new(ToolExecutionKind.Mcp, readOnly);

    private static void AssertBatches(
        IReadOnlyList<ToolExecutionBatch<int>> actual,
        params int[][] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i].Items.Select(x => x.Value));
        }
    }

    private sealed record Input(ToolExecutionKind Kind, bool ReadOnly);
}
