

using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.ChangePassword.Dtos;

public record ResetPasswordRequest
{
    [JsonPropertyName("oldPassword")]
    public required string OldPassword { get; init; }

    [JsonPropertyName("newPassword")]
    public required string NewPassword { get; init; }

    [JsonPropertyName("confirmPassword")]
    public required string ConfirmPassword { get; init; }
}
