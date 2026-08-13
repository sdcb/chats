using Chats.DB;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Mcp;

/// <summary>
/// Builds system-prompt segments from MCP server instructions without mutating stored ChatConfig.
/// </summary>
public static class McpServerInstructionsBuilder
{
    private static string? BuildInstructionsText(IEnumerable<McpServer> servers)
    {
        McpServer[] withInstructions = [.. servers
            .Where(s => !string.IsNullOrWhiteSpace(s.ServerInstructions))
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)];
        Dictionary<string, int> labelCounts = withInstructions
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        string[] parts = [.. withInstructions.Select(s =>
        {
            string displayLabel = labelCounts[s.Label] > 1 ? $"{s.Label} (#{s.Id})" : s.Label;
            return $"### MCP: {displayLabel}\n{s.ServerInstructions!.Trim()}";
        })];

        return parts.Length == 0 ? null : string.Join("\n\n", parts);
    }

    public static NeutralSystemMessage? MergeSystemMessage(
        NeutralSystemMessage? existingSystem,
        IEnumerable<McpServer> enabledMcpServers)
    {
        string? mcpInstructions = BuildInstructionsText(enabledMcpServers);
        if (mcpInstructions is null)
        {
            return existingSystem;
        }

        if (existingSystem is not null)
        {
            return NeutralSystemMessage.FromContents(
            [
                .. existingSystem.Contents,
                new NeutralSystemContent { Text = mcpInstructions },
            ]);
        }

        return NeutralSystemMessage.FromText(mcpInstructions);
    }
}
