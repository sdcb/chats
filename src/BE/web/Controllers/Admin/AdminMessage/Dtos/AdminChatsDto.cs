using Chats.BE.Controllers.Chats.UserChats.Dtos;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminMessage.Dtos;

public record AdminChatsDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("username")]
    public required string UserName { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("isDeleted")]
    public required bool IsDeleted { get; init; }

    [JsonPropertyName("isShared")]
    public required bool IsShared { get; init; }

    [JsonPropertyName("spans")]
    public required ChatSpanDto[] Spans { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }
}