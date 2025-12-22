using Chats.Web.Services.Common;
using System.Text.Json.Serialization;

namespace Chats.Web.DB.Jsons;

public record JsonModelKey
{
    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("secret")]
    public string? Secret { get; init; }

    public JsonModelKey WithMaskedKey()
    {
        return this with { Secret = Secret.JsonToMaskedNull() };
    }

    public bool IsMaskedEquals(JsonModelKey inputKey)
    {
        if (inputKey.Secret.SeemsMasked())
        {
            return WithMaskedKey() == inputKey;
        }
        else
        {
            return this == inputKey;
        }
    }
}
