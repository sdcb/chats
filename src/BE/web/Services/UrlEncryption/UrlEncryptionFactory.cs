
namespace Chats.BE.Services.UrlEncryption;

public class UrlEncryptionFactory(ILogger<UrlEncryptionFactory> logger, IConfiguration configuration)
{
    public IUrlEncryptionService Create()
    {
        string? idHasherPassword = configuration["ENCRYPTION_PASSWORD"];
        if (string.IsNullOrWhiteSpace(idHasherPassword))
        {
            logger.LogWarning("ENCRYPTION_PASSWORD is not set, using NoOpIdEncryptionService");
            return new NoOpUrlEncryptionService();
        }
        else
        {
            return new UrlEncryptionService(idHasherPassword);
        }
    }
}
