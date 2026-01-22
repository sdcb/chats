using Chats.BE.Infrastructure.Functional;

namespace Chats.BE.Services.UrlEncryption;

public class NoOpUrlEncryptionService : IUrlEncryptionService
{
    private static readonly byte[] hashKey = Utils.GenerateIdHasherKey(nameof(NoOpUrlEncryptionService), 32, 200);

    public int DecryptAsInt32(string encrypted, EncryptionPurpose purpose) => int.Parse(encrypted);

    public long DecryptAsInt64(string encrypted, EncryptionPurpose purpose) => long.Parse(encrypted);

    public string Encrypt(int id, EncryptionPurpose purpose) => id.ToString();

    public string Encrypt(long id, EncryptionPurpose purpose) => id.ToString();

    public string CreateSignedPath(TimedId timedId, EncryptionPurpose purpose)
    {
        string encryptedId = Encrypt(timedId.Id, purpose);
        string hash = Utils.SignData(timedId.Serialize(), hashKey);
        return $"{encryptedId}?validBefore={timedId.ValidBefore.ToUnixTimeMilliseconds()}&hash={hash}";
    }

    public Result<int> DecodeSignedPathAsInt32(string path, long validBefore, string hash, EncryptionPurpose purpose)
    {
        int id = DecryptAsInt32(path, purpose);
        DateTimeOffset validBeforeTime = DateTimeOffset.FromUnixTimeMilliseconds(validBefore);
        if (validBeforeTime < DateTimeOffset.UtcNow)
        {
            return Result.Fail<int>("The path has expired.");
        }

        string correctHash = Utils.SignData(new TimedId(id, validBeforeTime).Serialize(), hashKey);
        if (hash != correctHash)
        {
            return Result.Fail<int>("Invalid hash.");
        }

        return Result.Ok(id);
    }
}