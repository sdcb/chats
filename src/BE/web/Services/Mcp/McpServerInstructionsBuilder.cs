using System.Text;
using Chats.DB;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Mcp;

/// <summary>
/// Builds system-prompt segments from MCP server instructions without mutating stored ChatConfig.
/// </summary>
public static class McpServerInstructionsBuilder
{
    public static string? BuildInstructionsText(IEnumerable<McpServer> servers)
    {
        List<(string Label, string Instructions)> parts = servers
            .Where(s => !string.IsNullOrWhiteSpace(s.ServerInstructions))
            .Select(s => (s.Label, s.ServerInstructions!.Trim()))
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Label, StringComparer.Ordinal)
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new();
        sb.AppendLine("The following MCP server usage instructions apply to enabled tools:");
        sb.AppendLine();

        for (int i = 0; i < parts.Count; i++)
        {
            (string label, string instructions) = parts[i];
            sb.Append("### MCP: ");
            sb.AppendLine(label);
            sb.Append(instructions);
            if (i < parts.Count - 1)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public static NeutralSystemMessage? MergeSystemMessage(
        NeutralSystemMessage? existingSystem,
        string? chatConfigSystemPrompt,
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

        if (!string.IsNullOrWhiteSpace(chatConfigSystemPrompt))
        {
            return NeutralSystemMessage.FromContents(
                new NeutralSystemContent { Text = chatConfigSystemPrompt },
                new NeutralSystemContent { Text = mcpInstructions });
        }

        return NeutralSystemMessage.FromText(mcpInstructions);
    }
}
