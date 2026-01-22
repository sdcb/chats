using System.Text.Json.Serialization;
using Chats.BE.Services.Common;
using Chats.BE.Services.Keycloak;

namespace Chats.BE.DB.Jsons;

public record JsonKeycloakConfig
{
    [JsonPropertyName("wellKnown")]
    public required string WellKnown { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("secret")]
    public required string Secret { get; init; }

    public async Task<string> GenerateLoginUrl(string redirectUrl, CancellationToken cancellationToken)
    {
        KeycloakOAuthConfig config = await LoadWellknown(cancellationToken);
        string scope = "openid"; // Include any other scopes you need
        string authorizationEndpoint = config.AuthorizationEndpoint;
        return $"{authorizationEndpoint}?client_id={ClientId}&redirect_uri={redirectUrl}&response_type=code&scope={scope}";
    }

    public async Task<AccessTokenInfo> GetUserInfo(string code, string redirectUrl, CancellationToken cancellationToken)
    {
        KeycloakOAuthConfig config = await LoadWellknown(cancellationToken);

        using HttpClient httpClient = new();
        HttpResponseMessage resp = await httpClient.PostAsync(config.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["client_secret"] = Secret,
            ["code"] = code,
            ["redirect_uri"] = redirectUrl,
        }), cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to get access token: {await resp.Content.ReadAsStringAsync(cancellationToken)}");
        }

        SsoTokenDto tokenDto = (await resp.Content.ReadFromJsonAsync<SsoTokenDto>(cancellationToken))!;
        AccessTokenInfo info = AccessTokenInfo.Decode(tokenDto.AccessToken);
        return info;
    }

    private async Task<KeycloakOAuthConfig> LoadWellknown(CancellationToken cancellationToken)
    {
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync(WellKnown, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<KeycloakOAuthConfig>(cancellationToken))!;
        }

        throw new InvalidOperationException($"Failed to get Keycloak well-known configuration: {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }

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
