using Chats.DB;
using Chats.BE.DB.Jsons;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.InitialConfigs.Dtos;

public class UserInitialConfigUpdateRequest
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("loginType")]
    public required string LoginType { get; init; }

    [JsonPropertyName("models")]
    public required JsonTokenBalance[] Models { get; init; }

    [JsonPropertyName("price")]
    public required decimal Price { get; init; }

    [JsonPropertyName("invitationCodeId")]
    public required int? InvitationCodeId { get; init; }

    public void ApplyTo(UserInitialConfig config)
    {
        config.Name = Name;
        config.LoginType = LoginType;
        config.Models = JsonSerializer.Serialize(Models);
        config.Price = Price;
        config.InvitationCodeId = InvitationCodeId;
    }
}

public class UserInitialConfigCreateRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("loginType")]
    public required string LoginType { get; init; }

    [JsonPropertyName("models")]
    public required JsonTokenBalance[] Models { get; init; }

    [JsonPropertyName("price")]
    public required decimal Price { get; init; }

    [JsonPropertyName("invitationCodeId")]
    public required int? InvitationCodeId { get; init; }

    public void ApplyTo(UserInitialConfig config)
    {
        config.Name = Name;
        config.LoginType = LoginType;
        config.Models = JsonSerializer.Serialize(Models);
        config.Price = Price;
        config.InvitationCodeId = InvitationCodeId;
    }
}
