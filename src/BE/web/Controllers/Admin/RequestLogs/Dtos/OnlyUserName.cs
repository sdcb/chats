using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.RequestLogs.Dtos;

public class OnlyUserName
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }
}