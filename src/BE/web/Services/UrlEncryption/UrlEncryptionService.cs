using Chats.BE.Infrastructure.Functional;
using System.Security.Cryptography;

namespace Chats.BE.Services.UrlEncryption;

public class UrlEncryptionService : IUrlEncryptionService
{
    private readonly byte[] _key;
    private readonly Dictionary<EncryptionPurpose, byte[]> _ivs = [];

    public UrlEncryptionService(string idHasherPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idHasherPassword, nameof(idHasherPassword));
        _key = Utils.GenerateIdHasherKey(idHasherPassword, keyLength: 32, iterations: 200);
        foreach (EncryptionPurpose purpose in Enum.GetValues<EncryptionPurpose>())
        {
            _ivs[purpose] = Utils.GenerateIdHasherKey(idHasherPassword + purpose, keyLength: 16, iterations: 200);
        }
    }

    public string Encrypt(int id, EncryptionPurpose purpose) => Utils.Encrypt(BitConverter.GetBytes(id), _key, _ivs[purpose]);
    public string Encrypt(long id, EncryptionPurpose purpose) => Utils.Encrypt(BitConverter.GetBytes(id), _key, _ivs[purpose]);

    public int DecryptAsInt32(string encrypted, EncryptionPurpose purpose) => BitConverter.ToInt32(Utils.Decrypt(encrypted, _key, _ivs[purpose]));
    public long DecryptAsInt64(string encrypted, EncryptionPurpose purpose) => BitConverter.ToInt64(Utils.Decrypt(encrypted, _key, _ivs[purpose]));

    public string CreateSignedPath(TimedId timedId, EncryptionPurpose purpose)
    {
        string encryptedId = Encrypt(timedId.Id, purpose);
        string hash = Utils.SignData(timedId.Serialize(), _key);
        return $"{encryptedId}?validBefore={timedId.ValidBefore.ToUnixTimeMilliseconds()}&hash={hash}";
    }

    public Result<int> DecodeSignedPathAsInt32(string path, long validBefore, string hash, EncryptionPurpose purpose)
    {
        int id;
        try
        {
            id = DecryptAsInt32(path, purpose);
        }
        catch (CryptographicException)
        {
            return Result.Fail<int>("Invalid encrypted ID.");
        }
        DateTimeOffset validBeforeTime = DateTimeOffset.FromUnixTimeMilliseconds(validBefore);
        if (validBeforeTime < DateTimeOffset.UtcNow)
        {
            return Result.Fail<int>("The path has expired.");
        }

        string correctHash = Utils.SignData(new TimedId(id, validBeforeTime).Serialize(), _key);
        if (hash != correctHash)
        {
            return Result.Fail<int>("Invalid hash.");
        }

        return Result.Ok(id);
    }
}
