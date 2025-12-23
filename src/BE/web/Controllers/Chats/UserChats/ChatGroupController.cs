using Chats.DB;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.UserChats;

[Route("api/chat/group"), Authorize]
public class ChatGroupController(ChatsDB db, CurrentUser user, IUrlEncryptionService urlEncryption) : ControllerBase
{
    internal const int MaxGroupCount = 60;
    internal const short RankStep = 1000;
    internal const short RankStart = -30000;

    IOrderedQueryable<ChatGroup> UserOrderedChatGroups => db.ChatGroups
        .Where(x => x.UserId == user.Id)
        .OrderByDescending(x => x.Rank)
        .ThenByDescending(x => x.Id);

    [HttpGet]
    public async Task<ActionResult<ChatGroupDto[]>> ListGroups(CancellationToken cancellationToken)
    {
        ChatGroupDto[] groups = await UserOrderedChatGroups
            .Select(x => new ChatGroupDto
            {
                Id = urlEncryption.EncryptChatGroupId(x.Id),
                Name = x.Name,
                Rank = x.Rank,
                IsExpanded = x.IsExpanded,
            })
            .ToArrayAsync(cancellationToken);
        return Ok(groups);
    }

    [HttpGet("with-chats")]
    public async Task<ActionResult<ChatGroupDtoWithChats[]>> ListGroupsWithChats([FromQuery] QueryPagingRequest req, [FromServices] IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
    {
        List<ChatGroupDtoWithChats> groups = await UserOrderedChatGroups
            .Select(x => new ChatGroupDtoWithChats
            {
                Id = urlEncryption.EncryptChatGroupId(x.Id),
                Name = x.Name,
                Rank = x.Rank,
                IsExpanded = x.IsExpanded,
            })
            .ToListAsync(cancellationToken);
        groups.Add(new ChatGroupDtoWithChats()
        {
            Id = null!,
            Name = "Ungrouped",
            IsExpanded = true,
            Rank = 0,
        });

        await Parallel.ForEachAsync(groups, cancellationToken, async (group, ct) =>
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            using ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            group.Chats = await UserChatsController.GetChatsForGroupAsync(db, user, urlEncryption, new ChatsQuery(group.Id, req.Page, req.PageSize, req.Query), ct);
        });

        return Ok(groups);
    }

    [HttpPost]
    public async Task<ActionResult<ChatGroupDto>> CreateGroup([FromBody] CreateChatGroupRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Group name cannot be empty");
        }

        ChatGroup[] existingGroups = await UserOrderedChatGroups
            .ToArrayAsync(cancellationToken);

        if (existingGroups.Length >= MaxGroupCount)
        {
            return BadRequest("You have reached the maximum number of groups");
        }

        bool isDuplicatedName = existingGroups.Any(x => x.Name.Equals(req.Name, StringComparison.OrdinalIgnoreCase));
        if (isDuplicatedName)
        {
            return BadRequest("Group name already exists");
        }

        int sugguestedRank = existingGroups.Length > 0 ? existingGroups[0].Rank + RankStep : RankStart;
        if (sugguestedRank > short.MaxValue)
        {
            ReorderGroups(existingGroups);
            await db.SaveChangesAsync(cancellationToken);
            sugguestedRank = existingGroups[0].Rank + RankStep;
        }

        ChatGroup group = new()
        {
            Name = req.Name,
            UserId = user.Id,
            Rank = (short)sugguestedRank,
            IsExpanded = req.IsExpanded,
        };
        db.ChatGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ChatGroupDto
        {
            Id = urlEncryption.EncryptChatGroupId(group.Id),
            Name = group.Name,
            Rank = group.Rank,
            IsExpanded = group.IsExpanded,
        });
    }

    private static void ReorderGroups(ChatGroup[] existingGroups)
    {
        for (int i = 0; i < existingGroups.Length; i++)
        {
            int reverseIndex = existingGroups.Length - i - 1;
            existingGroups[reverseIndex].Rank = (short)(RankStart + i * RankStep);
        }
    }

    [HttpPut("{encryptedChatGroupId}")]
    public async Task<ActionResult> UpdateGroup(string encryptedChatGroupId, [FromBody] UpdateChatGroupRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(encryptedChatGroupId))
        {
            return BadRequest("Group ID cannot be empty");
        }

        int chatGroupId = urlEncryption.DecryptChatGroupId(encryptedChatGroupId);
        ChatGroup? group = await db.ChatGroups.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Id == chatGroupId, cancellationToken);
        if (group == null)
        {
            return NotFound();
        }
        req.ApplyTo(group);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok();
    }

    [HttpDelete("{encryptedChatGroupId}")]
    public async Task<ActionResult> DeleteGroup(string encryptedChatGroupId, CancellationToken cancellationToken)
    {
        int chatGroupId = urlEncryption.DecryptChatGroupId(encryptedChatGroupId);
        ChatGroup? group = await db.ChatGroups.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Id == chatGroupId, cancellationToken);
        if (group == null)
        {
            return NotFound();
        }
        db.ChatGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPut("move")]
    public async Task<ActionResult> MoveGroup([FromBody] EncryptedMoveChatGroupRequest encryptedRequest,
        [FromServices] IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        MoveChatGroupRequest req = encryptedRequest.Decrypt(urlEncryption);
        Result<MoveChatGroupContext> ctxResult = await req.Load(db, scopeFactory, user.Id, cancellationToken);
        if (ctxResult.IsFailure)
        {
            return BadRequest(ctxResult.Error);
        }

        MoveChatGroupContext ctx = ctxResult.Value;
        if (!ctx.ValidateBeforeAfterRank())
        {
            return BadRequest("Invalid move request");
        }

        bool needReorder = !ctx.ApplyMove();
        if (needReorder)
        {
            ChatGroup[] allGroups = await UserOrderedChatGroups.ToArrayAsync(cancellationToken);
            ReorderGroups(allGroups);
            ctx.Reload(allGroups);
            ctx.ApplyMove();
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
