using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.RequestLogs.Dtos;

public class OnlyUserName
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }
}