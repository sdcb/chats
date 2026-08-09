namespace Chats.BE.Services.Common;

public static class PasswordPolicy
{
    public const string ErrorMessage =
        "Password should be at least 8 characters long and contain at least three of the following: one lowercase letter, one uppercase letter, one digit, and one special character.";

    public static bool IsStrongEnough(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            return false;
        }

        bool hasLowercase = false;
        bool hasUppercase = false;
        bool hasDigit = false;
        bool hasSpecialChar = false;

        foreach (char c in password)
        {
            hasLowercase |= char.IsLower(c);
            hasUppercase |= char.IsUpper(c);
            hasDigit |= char.IsDigit(c);
            hasSpecialChar |= !char.IsLetterOrDigit(c);
        }

        int typesCount = (hasLowercase ? 1 : 0) + (hasUppercase ? 1 : 0) +
            (hasDigit ? 1 : 0) + (hasSpecialChar ? 1 : 0);
        return typesCount >= 3;
    }
}
