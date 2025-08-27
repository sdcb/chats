using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace Chats.BE.Services.Keycloak;

public record AccessTokenInfo
{
    [JsonPropertyName("sub")]
    public required string Sub { get; init; }

    [JsonPropertyName("given_name")]
    public required string GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public required string FamilyName { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    public string GetSuggestedUserName() => FamilyName + GivenName;

    public static AccessTokenInfo Decode(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentNullException(nameof(token));

        string[] parts = token.Split('.');
        if (parts.Length < 3)
            throw new ArgumentException("Invalid JWT token format.", nameof(token));

        string payload = parts[1];
        string decodedJson = Base64UrlDecode(payload);
        return JsonSerializer.Deserialize<AccessTokenInfo>(decodedJson) ?? throw new InvalidOperationException("Deserialization failed.");
    }

    private static string Base64UrlDecode(string input)
    {
        string output = input;
        output = output.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 0: break;
            case 2: output += "=="; break;
            case 3: output += "="; break;
            default: throw new FormatException("Invalid Base64 URL string.");
        }
        byte[] base64EncodedBytes = Convert.FromBase64String(output);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }
}

public record RealmAccess
{
    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }
}

public record ResourceAccess
{
    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }
}