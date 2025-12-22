using System.Configuration;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Public.AccountLogin.Dtos;

public record PhoneRegisterRequest
{
    [RegexStringValidator(@"^[1][3,4,5,6,7,8,9][0-9]{9}$")]
    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("smsCode")]
    public required string SmsCode { get; init; }

    [JsonPropertyName("invitationCode")]
    public required string InvitationCode { get; init; }
}