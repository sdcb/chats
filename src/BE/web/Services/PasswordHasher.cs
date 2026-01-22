namespace Chats.BE.Services;

public class PasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string? hash)
    {
        if (hash == null) return false;
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
