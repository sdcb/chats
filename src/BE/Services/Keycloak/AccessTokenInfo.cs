using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace Chats.BE.Services.Keycloak;

public record AccessTokenInfo
{
    [JsonPropertyName("sub")]
    public required string Sub { get; init; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    // 可选字段，一些 Keycloak 配置可能提供
    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    public string GetSuggestedUserName() 
    {
        // 优先保持现有逻辑：FamilyName + GivenName
        if (!string.IsNullOrWhiteSpace(FamilyName) && !string.IsNullOrWhiteSpace(GivenName))
            return FamilyName + GivenName;
            
        // 兼容性处理：当 FamilyName 或 GivenName 不存在时的备选方案
        if (!string.IsNullOrWhiteSpace(PreferredUsername))
            return PreferredUsername;
            
        if (!string.IsNullOrWhiteSpace(Name))
            return Name;
            
        // 使用 email 前缀
        if (!string.IsNullOrWhiteSpace(Email))
        {
            var emailParts = Email.Split('@');
            if (emailParts.Length > 0)
                return emailParts[0];
        }
            
        // 最后使用 sub
        return Sub;
    }

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