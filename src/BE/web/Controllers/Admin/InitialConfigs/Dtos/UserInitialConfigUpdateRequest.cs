using Chats.DB;
using Chats.BE.DB.Jsons;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.InitialConfigs.Dtos;

public abstract class UserInitialConfigRequest
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

    [JsonPropertyName("mcps")]
    public required JsonInitialMcp[] Mcps { get; init; }

    [JsonPropertyName("apiKeyEnabled")]
    public required bool ApiKeyEnabled { get; init; }

    public void ApplyTo(UserInitialConfig config)
    {
        config.Name = Name;
        config.LoginType = LoginType;
        config.Models = JsonSerializer.Serialize(Models);
        config.Price = Price;
        config.InvitationCodeId = InvitationCodeId;
        config.Mcps = JsonSerializer.Serialize(Mcps.Select(x => x.Normalize()));
        config.ApiKeyEnabled = ApiKeyEnabled;
    }
}

public class UserInitialConfigUpdateRequest : UserInitialConfigRequest
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
}

public class UserInitialConfigCreateRequest : UserInitialConfigRequest
{
}
