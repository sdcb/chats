using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;

namespace Chats.BE.UnitTest.Services.Sessions;

public class SessionEntryTests
{
    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void ToClaims_IncludesApiKeyPermission(bool enabled, string expected)
    {
        IUrlEncryptionService encryption = new NoOpUrlEncryptionService();
        SessionEntry entry = new()
        {
            UserId = 1,
            UserName = "user",
            Role = "-",
            ApiKeyEnabled = enabled,
        };

        Assert.Equal(expected, entry.ToClaims(encryption)
            .Single(x => x.Type == JwtPropertyKeys.ApiKeyEnabled).Value);
    }
}
