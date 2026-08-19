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
            .OrderBy(x => x.McpServer.Name, StringComparer.Ordinal)
            .ThenBy(x => x.ToolName, StringComparer.Ordinal)
            .ThenBy(x => x.Id)];
        HashSet<string> reserved = new(reservedNames, StringComparer.Ordinal);
        HashSet<string> usedNames = new(reserved, StringComparer.Ordinal);

        List<McpToolNameMapping> mappings = [];
        foreach (McpTool tool in orderedTools)
        {
            string exposedName = McpProtocolName.BuildExposedToolName(tool.McpServer.Name, tool.ToolName);
            if (!McpProtocolName.IsValidExposedToolName(exposedName))
            {
                throw new InvalidOperationException(
                    $"MCP tool '{tool.ToolName}' on server '{tool.McpServer.Name}' produces invalid protocol name '{exposedName}'.");
            }
            if (!usedNames.Add(exposedName))
            {
                throw new InvalidOperationException($"MCP tool protocol name '{exposedName}' is duplicated or reserved.");
            }

            mappings.Add(new McpToolNameMapping(tool, exposedName));
        }

        return mappings;
    }
}
