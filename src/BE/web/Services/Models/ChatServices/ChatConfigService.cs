using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Models.ChatServices;

public class ChatConfigService(ChatsDB db)
{
    public async Task<ChatConfigSnapshot> GetOrCreateChatConfigSnapshot(ChatConfig raw, CancellationToken cancellationToken)
    {
        int modelSnapshotId = raw.Model?.CurrentSnapshotId
            ?? await db.Models
                .Where(x => x.Id == raw.ModelId)
                .Select(x => x.CurrentSnapshotId)
                .SingleAsync(cancellationToken);

        string? enabledMcpNames = await GetEnabledMcpNames(raw, cancellationToken);

        ChatConfigSnapshot? matchingConfig = await db.ChatConfigSnapshots
            .Where(c =>
                c.ModelSnapshotId == modelSnapshotId &&
                c.SystemPrompt == raw.SystemPrompt &&
                c.WebSearchEnabled == raw.WebSearchEnabled &&
                c.ReasoningEffortId == raw.ReasoningEffortId &&
                c.Temperature == raw.Temperature &&
                c.ImageSize == raw.ImageSize &&
                c.CodeExecutionEnabled == raw.CodeExecutionEnabled &&
                c.MaxOutputTokens == raw.MaxOutputTokens &&
                c.ThinkingBudget == raw.ThinkingBudget &&
                c.EnabledMcpNames == enabledMcpNames)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (matchingConfig is not null)
        {
            return matchingConfig;
        }

        ChatConfigSnapshot newConfig = new()
        {
            ModelSnapshotId = modelSnapshotId,
            SystemPrompt = raw.SystemPrompt,
            Temperature = raw.Temperature,
            WebSearchEnabled = raw.WebSearchEnabled,
            MaxOutputTokens = raw.MaxOutputTokens,
            ReasoningEffortId = raw.ReasoningEffortId,
            CodeExecutionEnabled = raw.CodeExecutionEnabled,
            ImageSize = raw.ImageSize,
            ThinkingBudget = raw.ThinkingBudget,
            EnabledMcpNames = enabledMcpNames,
            HashCode = null,
            CreatedAt = DateTime.UtcNow,
        };

        db.ChatConfigSnapshots.Add(newConfig);
        await db.SaveChangesAsync(cancellationToken);
        return newConfig;
    }

    private async Task<string?> GetEnabledMcpNames(ChatConfig raw, CancellationToken cancellationToken)
    {
        int[] mcpIds;
        if (raw.ChatConfigMcps.Count > 0)
        {
            mcpIds = [.. raw.ChatConfigMcps.Select(x => x.McpServerId).Distinct()];
        }
        else if (raw.Id != 0)
        {
            mcpIds = [.. await db.ChatConfigMcps
                .Where(x => x.ChatConfigId == raw.Id)
                .Select(x => x.McpServerId)
                .Distinct()
                .ToArrayAsync(cancellationToken)];
        }
        else
        {
            mcpIds = [];
        }

        if (mcpIds.Length == 0)
        {
            return null;
        }

        string[] labels = [.. await db.McpServers
            .Where(x => mcpIds.Contains(x.Id))
            .Select(x => x.Label)
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken)];

        return labels.Length == 0 ? null : string.Join(',', labels);
    }
}
