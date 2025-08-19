using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.UrlEncryption;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.UserChats.Dtos;

public class UpdateChatsRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; } = null!;

    [JsonPropertyName("isArchived")]
    public bool? IsArchived { get; set; }

    [JsonPropertyName("isTopMost")]
    public bool? IsTopMost { get; set; }

    [JsonPropertyName("setsLeafMessageId")]
    public bool SetsLeafMessageId { get; set; }

    [JsonPropertyName("leafMessageId")]
    public string? LeafMessageId { get; set; } = null!;

    [JsonPropertyName("setsGroupId")]
    public bool SetsGroupId { get; set; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; } = null!;

    public DecryptedUpdateChatsRequest Decrypt(IUrlEncryptionService urlEncryptionService)
    {
        return new DecryptedUpdateChatsRequest
        {
            Title = Title,
            IsArchived = IsArchived,
            IsTopMost = IsTopMost,
            SetsLeafTurnId = SetsLeafMessageId,
            LeafTurnId = urlEncryptionService.DecryptTurnIdOrNull(LeafMessageId),
            SetsGroupId = SetsGroupId,
            GroupId = urlEncryptionService.DecryptChatGroupIdOrNull(GroupId),
        };
    }
}


public class DecryptedUpdateChatsRequest
{
    public string? Title { get; set; }

    public bool? IsArchived { get; set; }

    public bool? IsTopMost { get; set; }

    public bool SetsLeafTurnId { get; set; }

    public long? LeafTurnId { get; set; }

    public bool SetsGroupId { get; set; }

    public int? GroupId { get; set; }

    public async Task<string?> Validate(ChatsDB db, int chatId, CurrentUser currentUser)
    {
        if (Title != null && Title.Length > 50)
        {
            return "Title is too long";
        }

        if (SetsLeafTurnId && LeafTurnId != null)
        {
            if (!await db.ChatTurns.AnyAsync(x => x.Id == LeafTurnId && x.ChatId == chatId))
            {
                return "Leaf message not found";
            }
        }

        if (SetsGroupId && GroupId != null)
        {
            if (!await db.ChatGroups.AnyAsync(x => x.Id == GroupId && x.UserId == currentUser.Id))
            {
                return "Group not found";
            }
        }

        return null;
    }

    public void ApplyToChats(Chat chat)
    {
        if (Title != null)
        {
            chat.Title = Title;
        }
        if (IsArchived != null)
        {
            chat.IsArchived = IsArchived.Value;
        }
        if (SetsLeafTurnId)
        {
            chat.LeafMessageId = LeafTurnId;
        }
        if (IsTopMost != null)
        {
            chat.IsTopMost = IsTopMost.Value;
        }
        if (SetsGroupId)
        {
            chat.ChatGroupId = GroupId;
        }
    }
}