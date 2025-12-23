using Chats.DB;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.UserChats.Dtos;

public record ChatShareDto
{
    [JsonPropertyName("shareId")]
    public required string ShareId { get; init; }

    [JsonPropertyName("expiresAt")]
    public required DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("snapshotTime")]
    public required DateTime SnapshotTime { get; init; }

    public static ChatShareDto FromDB(ChatShare chatShare, IUrlEncryptionService urlEncryption)
    {
        return new ChatShareDto
        {
            ShareId = urlEncryption.EncryptChatShareId(chatShare.Id),
            ExpiresAt = chatShare.ExpiresAt,
            SnapshotTime = chatShare.SnapshotTime,
        };
    }

    public string ToUrl(HostUrlService hostUrlService)
    {
        return $"{hostUrlService.GetFEUrl()}/share/{ShareId}";
    }
}
