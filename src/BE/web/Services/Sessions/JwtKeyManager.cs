namespace Chats.Web.Services.Sessions;

public class JwtKeyManager(IConfiguration configuration)
{
    string _generated = Guid.NewGuid().ToString();

    public string GetOrCreateSecretKey()
    {
        string? secretKey = configuration["JwtSecretKey"];
        if (!string.IsNullOrEmpty(secretKey))
        {
            return secretKey;
        }
        else
        {
            return _generated;
        }
    }
}
