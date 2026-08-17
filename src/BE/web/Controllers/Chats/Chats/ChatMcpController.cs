using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Chats.BE.Services.Mcp;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chat/{encryptedChatId}/mcp"), Authorize]
public class ChatMcpController(ChatsDB db, IUrlEncryptionService idEncryption, CurrentUser currentUser) : ControllerBase
{
    [HttpPut("{mcpServerId:int}")]
    public async Task<ActionResult<ChatSpanDto[]>> Enable(
        string encryptedChatId,
        int mcpServerId,
        CancellationToken cancellationToken)
    {
        return await SetEnabled(encryptedChatId, mcpServerId, true, cancellationToken);
    }

    [HttpDelete("{mcpServerId:int}")]
    public async Task<ActionResult<ChatSpanDto[]>> Disable(
        string encryptedChatId,
        int mcpServerId,
        CancellationToken cancellationToken)
    {
        return await SetEnabled(encryptedChatId, mcpServerId, false, cancellationToken);
    }

    private async Task<ActionResult<ChatSpanDto[]>> SetEnabled(
        string encryptedChatId,
        int mcpServerId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        bool hasMcpAccess = await db.UserMcps.AnyAsync(
            x => x.UserId == currentUser.Id && x.McpServerId == mcpServerId,
            cancellationToken);
        if (!hasMcpAccess)
        {
            return BadRequest("Invalid MCP server permission");
        }

        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans)
                .ThenInclude(x => x.ChatConfig)
                    .ThenInclude(x => x.ChatConfigMcps)
            .Include(x => x.ChatSpans)
                .ThenInclude(x => x.ChatConfig.Model.CurrentSnapshot)
                    .ThenInclude(x => x.ModelKeySnapshot)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                x => x.Id == chatId && x.UserId == currentUser.Id && !x.IsArchived,
                cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        ChatConfig[] targetConfigs = [.. chat.ChatSpans
            .Where(x => x.ChatConfig.Model.CurrentSnapshot.AllowToolCall)
            .GroupBy(x => x.ChatConfigId)
            .Select(x => x.First().ChatConfig)];

        if (enabled)
        {
            foreach (ChatConfig config in targetConfigs)
            {
                string? conflict = await McpServerNameConflictValidator.FindConflictAsync(
                    db,
                    config.ChatConfigMcps.Select(x => x.McpServerId).Append(mcpServerId),
                    cancellationToken);
                if (conflict is not null)
                {
                    return BadRequest(conflict);
                }
            }
        }

        ApplyMcpState(targetConfigs, mcpServerId, enabled);

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        ChatSpanDto[] result = [.. chat.ChatSpans
            .OrderBy(x => x.SpanId)
            .Select(ChatSpanDto.FromDB)];
        return Ok(result);
    }

    internal static void ApplyMcpState(IEnumerable<ChatConfig> targetConfigs, int mcpServerId, bool enabled)
    {
        foreach (ChatConfig config in targetConfigs)
        {
            ChatConfigMcp[] existingAssociations = [.. config.ChatConfigMcps
                .Where(x => x.McpServerId == mcpServerId)
                .OrderBy(x => x.Id)];

            if (enabled && existingAssociations.Length == 0)
            {
                config.ChatConfigMcps.Add(new ChatConfigMcp
                {
                    ChatConfig = config,
                    McpServerId = mcpServerId,
                });
                continue;
            }

            int firstAssociationToRemove = enabled ? 1 : 0;
            foreach (ChatConfigMcp association in existingAssociations.Skip(firstAssociationToRemove))
            {
                config.ChatConfigMcps.Remove(association);
            }
        }
    }
}
