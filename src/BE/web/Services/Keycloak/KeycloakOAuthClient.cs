using Chats.BE.DB.Jsons;
using Chats.BE.Services.Common;

namespace Chats.BE.Services.Keycloak;

public class KeycloakOAuthClient(IHttpClientFactory httpClientFactory)
{
    public async Task<string> GenerateLoginUrl(JsonKeycloakConfig config, string redirectUrl, CancellationToken cancellationToken)
    {
        KeycloakOAuthConfig oauth = await LoadWellknown(config.WellKnown, cancellationToken);
        string scope = "openid";
        string authorizationEndpoint = oauth.AuthorizationEndpoint;
        return $"{authorizationEndpoint}?client_id={config.ClientId}&redirect_uri={redirectUrl}&response_type=code&scope={scope}";
    }

    public async Task<AccessTokenInfo> GetUserInfo(JsonKeycloakConfig config, string code, string redirectUrl, CancellationToken cancellationToken)
    {
        KeycloakOAuthConfig oauth = await LoadWellknown(config.WellKnown, cancellationToken);

        using HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage resp = await httpClient.PostAsync(oauth.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.Secret,
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

    private async Task<KeycloakOAuthConfig> LoadWellknown(string wellKnownUrl, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage response = await httpClient.GetAsync(wellKnownUrl, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<KeycloakOAuthConfig>(cancellationToken))!;
        }

        throw new InvalidOperationException($"Failed to get Keycloak well-known configuration: {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }
}
