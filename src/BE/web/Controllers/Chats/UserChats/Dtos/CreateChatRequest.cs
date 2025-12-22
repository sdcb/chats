using Chats.Web.Services.UrlEncryption;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.UserChats.Dtos;

public record EncryptedCreateChatRequest
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("groupId")]
    public required string? GroupId { get; init; }

    public CreateChatRequest Decrypt(IUrlEncryptionService urlEncryption)
    {
        return new CreateChatRequest
        {
            Title = Title,
            GroupId = urlEncryption.DecryptChatGroupIdOrNull(GroupId)
        };
    }
}

public record CreateChatRequest
{
    public required string Title { get; init; }

    public required int? GroupId { get; init; }
}