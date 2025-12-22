using Chats.Web.DB;
using Chats.Web.Infrastructure.Functional;
using Chats.Web.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Chats.Web.Controllers.Chats.UserChats.Dtos;

public record EncryptedMoveChatGroupRequest
{
    [JsonPropertyName("groupId")]
    public required string GroupId { get; init; }

    [JsonPropertyName("beforeGroupId")]
    public required string? BeforeGroupId { get; init; }

    [JsonPropertyName("afterGroupId")]
    public required string? AfterGroupId { get; init; }

    public MoveChatGroupRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new MoveChatGroupRequest
        {
            GroupId = urlEncryption.DecryptChatGroupId(GroupId),
            BeforeGroupId = urlEncryption.DecryptChatGroupIdOrNull(BeforeGroupId),
            AfterGroupId = urlEncryption.DecryptChatGroupIdOrNull(AfterGroupId),
        };
    }
}

public record MoveChatGroupRequest
{
    public required int GroupId { get; init; }

    public required int? BeforeGroupId { get; init; }

    public required int? AfterGroupId { get; init; }

    public async Task<Result<MoveChatGroupContext>> Load(ChatsDB primaryDB, IServiceScopeFactory serviceScopeFactory, int userId, CancellationToken cancellationToken)
    {
        Task<ChatGroup?> chatGroupTask = primaryDB.ChatGroups.FirstOrDefaultAsync(x => x.Id == GroupId && x.UserId == userId, cancellationToken);
        Task<ChatGroup?> beforeGroupTask = BeforeGroupId.HasValue ? LoadChatGroup(BeforeGroupId.Value, userId) : Task.FromResult<ChatGroup?>(null);
        Task<ChatGroup?> afterGroupTask = AfterGroupId.HasValue ? LoadChatGroup(AfterGroupId.Value, userId) : Task.FromResult<ChatGroup?>(null);

        ChatGroup? group = await chatGroupTask;
        if (group == null)
        {
            return Result.Fail<MoveChatGroupContext>("Chat group not found");
        }

        await Task.WhenAll(chatGroupTask, beforeGroupTask, afterGroupTask);

        return Result.Ok(new MoveChatGroupContext
        {
            Group = group,
            BeforeGroup = beforeGroupTask.Result,
            AfterGroup = afterGroupTask.Result,
        });


        async Task<ChatGroup?> LoadChatGroup(int groupId, int userId)
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            using ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            return await db.ChatGroups.FirstOrDefaultAsync(x => x.Id == groupId && x.UserId == userId, cancellationToken);
        }
    }
}

public record MoveChatGroupContext
{
    public required ChatGroup Group { get; set; }

    public required ChatGroup? BeforeGroup { get; set; }

    public required ChatGroup? AfterGroup { get; set; }

    public void Reload(ChatGroup[] allGroups)
    {
        Group = allGroups.First(x => x.Id == Group.Id);
        BeforeGroup = BeforeGroup != null ? allGroups.First(x => x.Id == BeforeGroup.Id) : null;
        AfterGroup = AfterGroup != null ? allGroups.First(x => x.Id == AfterGroup.Id) : null;
    }

    public bool ValidateBeforeAfterRank()
    {
        // rank is ordered from largest to smallest
        // ensure before rank is lesser or equals than after rank
        // before group and after group can't be null at the same time
        if (BeforeGroup == null && AfterGroup == null)
        {
            return false;
        }

        if (BeforeGroup != null && AfterGroup != null && BeforeGroup.Rank >= AfterGroup.Rank)
        {
            return false;
        }

        return true;
    }

    public bool ApplyMove()
    {
        // move the group into center of before and after group
        // if no gap, return false(means need reorder), otherwise return true(means success)
        // throw exception when assert failed
        if (!ValidateBeforeAfterRank())
        {
            throw new InvalidOperationException("Invalid move request");
        }

        int newRank = 0;
        if (BeforeGroup != null && AfterGroup != null)
        {
            if (BeforeGroup.Rank + 1 == AfterGroup.Rank)
            {
                return false;
            }
            newRank = (short)((BeforeGroup.Rank + AfterGroup.Rank) / 2);
        }
        else if (BeforeGroup != null)
        {
            newRank = (short)(BeforeGroup.Rank + ChatGroupController.RankStep);
        }
        else if (AfterGroup != null)
        {
            newRank = (short)(AfterGroup.Rank - ChatGroupController.RankStep);
        }

        if (newRank > short.MaxValue || newRank < short.MinValue)
        {
            return false;
        }

        Group.Rank = (short)newRank;
        return true;
    }
}