using Chats.Web.Controllers.Common.Dtos;
using Chats.Web.DB;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Chats.UserChats.Dtos;

public record ChatGroupDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("rank")]
    public required short Rank { get; init; }

    [JsonPropertyName("isExpanded")]
    public required bool IsExpanded { get; init; }
}

public record ChatGroupDtoWithChats : ChatGroupDto
{
    [JsonPropertyName("chats")]
    public PagedResult<ChatsResponse> Chats { get; set; } = null!;
}

public record CreateChatGroupRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("isExpanded")]
    public required bool IsExpanded { get; init; }
}

public record UpdateChatGroupRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("rank")]
    public short? Rank { get; init; }

    [JsonPropertyName("isExpanded")]
    public bool? IsExpanded { get; init; }

    public void ApplyTo(ChatGroup group)
    {
        if (Name is not null)
        {
            group.Name = Name;
        }
        if (Rank is not null)
        {
            group.Rank = Rank.Value;
        }
        if (IsExpanded is not null)
        {
            group.IsExpanded = IsExpanded.Value;
        }
    }
}