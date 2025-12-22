using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Public.AccountLogin.Dtos;

public record LoginRequest
{
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("username")]
    public string? UserName { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    public object AsLoginDto()
    {
        return this switch
        {
            { UserName: not null, Password: not null } => new PasswordLoginRequest
            {
                UserName = UserName!,
                Password = Password!
            },
            { Code: not null, Provider: not null } => new SsoLoginRequest
            {
                Provider = Provider!,
                Code = Code!
            },
            _ => throw new ArgumentException("Invalid login request")
        };
    }
}
