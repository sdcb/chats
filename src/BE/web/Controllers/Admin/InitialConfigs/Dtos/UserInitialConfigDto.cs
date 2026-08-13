using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Chats.BE.DB.Jsons;

namespace Chats.BE.Controllers.Admin.InitialConfigs.Dtos;

public class UserInitialConfigDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("loginType")]
    public required string LoginType { get; init; }

    [JsonPropertyName("models")]
    public required JsonNode[] Models { get; init; }

    [JsonPropertyName("price")]
    public required string Price { get; init; }

    [JsonPropertyName("invitationCodeId")]
    public required string InvitationCodeId { get; init; }

    [JsonPropertyName("invitationCode")]
    public required string InvitationCode { get; init; }

    [JsonPropertyName("mcps")]
    public required JsonInitialMcp[] Mcps { get; init; }

    [JsonPropertyName("apiKeyEnabled")]
    public required bool ApiKeyEnabled { get; init; }
}

public class UserInitialConfigDtoTemp
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string LoginType { get; init; }

    public required string Models { get; init; }

    public required decimal Price { get; init; }

    public required int? InvitationCodeId { get; init; }

    public required string InvitationCode { get; init; }

    public required string Mcps { get; init; }

    public required bool ApiKeyEnabled { get; init; }

    internal UserInitialConfigDto ToDto()
    {
        return new UserInitialConfigDto
        {
            Id = Id,
            Name = Name,
            LoginType = LoginType,
            Models = [.. JsonSerializer.Deserialize<JsonArray>(Models)!.Select(x => x!)],
            Price = Price.ToString(),
            InvitationCodeId = InvitationCodeId?.ToString() ?? "-",
            InvitationCode = InvitationCode,
            Mcps = JsonSerializer.Deserialize<JsonInitialMcp[]>(Mcps) ?? [],
            ApiKeyEnabled = ApiKeyEnabled,
        };
    }
}
