using Chats.DB.Enums;
using System.Configuration;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Public.SMSs.Dtos;

public record SmsRequest
{
    [RegexStringValidator(@"^[1][3,4,5,6,7,8,9][0-9]{9}$")]
    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("type")]
    public required DBSmsType Type { get; init; }

    [JsonPropertyName("invitationCode")]
    public string? InvitationCode { get; init; }
}