using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Mcp;

public static class McpServerNameConflictValidator
{
    public static async Task<string?> FindConflictAsync(
        ChatsDB db,
        IEnumerable<int> serverIds,
        CancellationToken cancellationToken)
    {
        int[] ids = [.. serverIds.Distinct()];
        if (ids.Length < 2) return null;

        string[] names = await db.McpServers
            .Where(x => ids.Contains(x.Id))
            .Select(x => x.Name)
            .ToArrayAsync(cancellationToken);
        string? duplicate = names
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return duplicate is null
            ? null
            : $"Multiple MCP servers named '{duplicate}' cannot be enabled in the same chat configuration.";
    }

    public static async Task<bool> HasRenameConflictAsync(
        ChatsDB db,
        int serverId,
        string newName,
        CancellationToken cancellationToken)
    {
        string normalizedName = newName.ToUpper();
        return await db.ChatConfigMcps.AnyAsync(current =>
            current.McpServerId == serverId &&
            db.ChatConfigMcps.Any(other =>
                other.ChatConfigId == current.ChatConfigId &&
                other.McpServerId != serverId &&
                other.McpServer.Name.ToUpper() == normalizedName),
            cancellationToken);
    }
}
