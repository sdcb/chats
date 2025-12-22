using Chats.Web.DB;
using Chats.Web.Services;
using System.Text.Json.Serialization;

namespace Chats.Web.Controllers.Admin.AdminUser.Dtos;

public record UpdateUserDto
{
    [JsonPropertyName("id")]
    public required int UserId { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("username")]
    public string? UserName { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    public void ApplyToUser(User user, PasswordHasher passwordHasher)
    {
        if (Email != null)
        {
            user.Email = Email;
        }
        if (Enabled != null)
        {
            user.Enabled = Enabled.Value;
        }
        if (Phone != null)
        {
            user.Phone = Phone;
        }
        if (Role != null)
        {
            user.Role = Role;
        }
        if (Avatar != null)
        {
            user.Avatar = Avatar;
        }
        if (UserName != null)
        {
            user.DisplayName = UserName;
        }
        if (!string.IsNullOrEmpty(Password))
        {
            user.PasswordHash = passwordHasher.HashPassword(Password);
        }
    }
}