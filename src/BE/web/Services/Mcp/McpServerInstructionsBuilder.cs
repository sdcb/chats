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
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)];
        string[] parts = [.. withInstructions.Select(s =>
        {
            string title = string.IsNullOrWhiteSpace(s.DisplayName) ||
                string.Equals(s.DisplayName, s.Name, StringComparison.Ordinal)
                ? s.Name
                : $"{s.DisplayName} ({s.Name})";
            return $"### MCP: {title}\n{s.ServerInstructions!.Trim()}";
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
