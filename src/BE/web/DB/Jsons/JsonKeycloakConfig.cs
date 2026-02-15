using System.Text.Json.Serialization;
using Chats.BE.Services.Common;

namespace Chats.BE.DB.Jsons;

public record JsonKeycloakConfig
{
    [JsonPropertyName("wellKnown")]
    public required string WellKnown { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("secret")]
    public required string Secret { get; init; }

    public JsonKeycloakConfig WithMaskedSecret()
    {
        return this with { Secret = Secret.ToMasked() };
    }

    public bool IsMaskedEquals(JsonKeycloakConfig other)
    {
        if (other.Secret.SeemsMasked())
        {
            return WithMaskedSecret() == other;
        }
        else
        {
            return this == other;
        }
    }
}
