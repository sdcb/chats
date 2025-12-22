using System.Buffers.Binary;

namespace Chats.Web.Services.UrlEncryption;

public record TimedId(int Id, DateTimeOffset ValidBefore)
{
    public static TimedId CreateFor(int id, TimeSpan validTime)
    {
        return new TimedId(id, DateTimeOffset.UtcNow + validTime);
    }

    public byte[] Serialize()
    {
        byte[] bytes = new byte[4 + 8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, Id);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(4), ValidBefore.ToUnixTimeMilliseconds());
        return bytes;
    }
}
