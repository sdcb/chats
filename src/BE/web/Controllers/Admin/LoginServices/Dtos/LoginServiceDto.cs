using Chats.Web.DB.Jsons;
using Chats.Web.Services.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.LoginServices.Dtos;

public record LoginServiceDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("configs")]
    public required object Configs { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }
}


public record LoginServiceDtoTemp
{
    public required int Id { get; init; }

    public required string Type { get; init; }

    public required string Configs { get; init; }

    public required bool Enabled { get; init; }

    public required DateTime CreatedAt { get; init; }

    public LoginServiceDto ToDto()
    {
        return new LoginServiceDto
        {
            Id = Id,
            Type = Type,
            Configs = Type switch
            {
                KnownLoginProviders.Keycloak => JsonSerializer.Deserialize<JsonKeycloakConfig>(Configs)!.WithMaskedSecret(), 
                KnownLoginProviders.Phone => (JsonObject)JsonNode.Parse(Configs)!,
                _ => (JsonObject)JsonNode.Parse(Configs)!,
            },
            Enabled = Enabled,
            CreatedAt = CreatedAt
        };
    }
}