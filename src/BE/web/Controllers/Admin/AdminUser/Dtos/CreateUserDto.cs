using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.AdminUser.Dtos;

public record CreateUserDto
{
    [JsonPropertyName("username")]
    public required string UserName { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("phone")]
    public required string Phone { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }
}
