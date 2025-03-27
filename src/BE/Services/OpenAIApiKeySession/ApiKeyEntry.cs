using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using System.Security.Claims;

namespace Chats.BE.Services.OpenAIApiKeySession;

public record ApiKeyEntry : SessionEntry
{
    public required int ApiKeyId { get; init; }
    public required string ApiKey { get; init; }
    public required DateTime Expires { get; init; }

    public override List<Claim> ToClaims(IUrlEncryptionService idEncryption)
    {
        return
        [
            ..base.ToClaims(idEncryption),
            new Claim("api-key", ApiKey),
            new Claim("api-key-id", ApiKeyId.ToString())
        ];
    }
}
