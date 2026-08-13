using Chats.DB;

namespace Chats.BE.Services.Mcp;

public sealed record McpToolNameMapping(McpTool Tool, string ExposedName);

public static class McpToolNameMapper
{
    public static IReadOnlyList<McpToolNameMapping> Build(
        IEnumerable<McpTool> tools,
        IEnumerable<string> reservedNames)
    {
        McpTool[] orderedTools = [.. tools
            .OrderBy(x => x.McpServerId)
            .ThenBy(x => x.ToolName, StringComparer.Ordinal)
            .ThenBy(x => x.Id)];
        HashSet<string> reserved = new(reservedNames, StringComparer.Ordinal);
        Dictionary<string, int> counts = orderedTools
            .GroupBy(x => x.ToolName, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

        HashSet<string> usedNames = new(reserved, StringComparer.Ordinal);
        foreach (McpTool tool in orderedTools)
        {
            if (counts[tool.ToolName] == 1 && !reserved.Contains(tool.ToolName))
            {
                usedNames.Add(tool.ToolName);
            }
        }

        List<McpToolNameMapping> mappings = [];
        foreach (McpTool tool in orderedTools)
        {
            string exposedName;
            if (counts[tool.ToolName] == 1 && !reserved.Contains(tool.ToolName))
            {
                exposedName = tool.ToolName;
            }
            else
            {
                string baseName = $"mcp_{tool.McpServerId}_{tool.ToolName}";
                exposedName = baseName;
                int suffix = 2;
                while (!usedNames.Add(exposedName))
                {
                    exposedName = $"{baseName}_{suffix++}";
                }
            }

            mappings.Add(new McpToolNameMapping(tool, exposedName));
        }

        return mappings;
    }
}
