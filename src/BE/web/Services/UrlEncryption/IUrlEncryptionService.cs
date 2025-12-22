using Chats.Web.Infrastructure.Functional;

namespace Chats.Web.Services.UrlEncryption;

public interface IUrlEncryptionService
{
    int DecryptAsInt32(string encrypted, EncryptionPurpose purpose);
    long DecryptAsInt64(string encrypted, EncryptionPurpose purpose);
    string Encrypt(int id, EncryptionPurpose purpose);
    string Encrypt(long id, EncryptionPurpose purpose);
    string CreateSignedPath(TimedId timedId, EncryptionPurpose purpose);
    Result<int> DecodeSignedPathAsInt32(string path, long validBefore, string hash, EncryptionPurpose purpose);
}