using Chats.DB;
using Chats.BE.DB.Jsons;
using Chats.BE.Services.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.LoginServices.Dtos;

public record LoginServiceUpdateRequest
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("configs")]
    public required string Configs { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    public void ApplyTo(LoginService entity)
    {
        entity.Type = Type;
        if (Type == KnownLoginProviders.Keycloak)
        {
            try
            {
                JsonKeycloakConfig newConfig = JsonSerializer.Deserialize<JsonKeycloakConfig>(Configs)!;
                JsonKeycloakConfig oldConfig = JsonSerializer.Deserialize<JsonKeycloakConfig>(entity.Configs)!;
                if (!oldConfig.IsMaskedEquals(newConfig))
                {
                    entity.Configs = Configs;
                }
            }
            catch (Exception)
            {
                entity.Configs = Configs;
            }
        }
        else
        {
            entity.Configs = Configs;
        }
        entity.Enabled = Enabled;
    }
}