using Chats.BE.Services;
using Chats.BE.Tests.Common;

namespace Chats.BE.Tests.Services;

public class CsrfTokenServiceTests
{
    [Fact]
    public void Constructor_SigningKeyNotSet_GeneratesNewKeyAndLogsWarning()
    {
        // Arrange
        DictionaryConfiguration config = new DictionaryConfiguration([]);
        StringLogger<CsrfTokenService> logger = new StringLogger<CsrfTokenService>();
        FixedTimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        // Act
        CsrfTokenService service = new CsrfTokenService(config, logger, timeProvider);

        // Assert
        string logs = logger.GetLogs();
        Assert.Contains("No SigningKey found in configuration", logs);
        Assert.Equal(32, GetPrivateField<byte[]>(service, "_key").Length);
    }

    [Fact]
    public void Constructor_SigningKeySet_ThrowsIfIncorrectLength()
    {
        // Arrange
        DictionaryConfiguration config = new DictionaryConfiguration(new Dictionary<string, string>
            {
                { "SigningKey", "TooShort" }
            });
        StringLogger<CsrfTokenService> logger = new StringLogger<CsrfTokenService>();
        FixedTimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CsrfTokenService(config, logger, timeProvider));
    }

    [Fact]
    public void GenerateToken_CreatesValidToken()
    {
        // Arrange
        DictionaryConfiguration config = new DictionaryConfiguration(new Dictionary<string, string>
            {
                { "SigningKey", "0UrY1tx6Z6GAQKX/xsC1xjQ3uaMHaEs3cRf8kwgEz+Q=" } // This should be a 32-byte base64 encoded key
            });
        StringLogger<CsrfTokenService> logger = new StringLogger<CsrfTokenService>();
        FixedTimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        CsrfTokenService service = new CsrfTokenService(config, logger, timeProvider);

        // Act
        string token = service.GenerateToken();

        // Assert
        Assert.NotNull(token);
        byte[] tokenBytes = Convert.FromBase64String(token);
        Assert.Equal(40, tokenBytes.Length); // 8 bytes timestamp + 32 bytes hash
    }

    [Fact]
    public void VerifyToken_ValidToken_ReturnsTrue()
    {
        // Arrange
        DictionaryConfiguration config = new DictionaryConfiguration(new Dictionary<string, string>
            {
                { "SigningKey", "0UrY1tx6Z6GAQKX/xsC1xjQ3uaMHaEs3cRf8kwgEz+Q=" }
            });
        StringLogger<CsrfTokenService> logger = new StringLogger<CsrfTokenService>();
        FixedTimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        CsrfTokenService service = new CsrfTokenService(config, logger, timeProvider);
        string token = service.GenerateToken();

        // Act
        bool result = service.VerifyToken(token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyToken_InvalidToken_ReturnsFalse()
    {
        // Arrange
        DictionaryConfiguration config = new DictionaryConfiguration(new Dictionary<string, string>
            {
                { "SigningKey", "0UrY1tx6Z6GAQKX/xsC1xjQ3uaMHaEs3cRf8kwgEz+Q=" }
            });
        StringLogger<CsrfTokenService> logger = new StringLogger<CsrfTokenService>();
        FixedTimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        CsrfTokenService service = new CsrfTokenService(config, logger, timeProvider);
        string token = service.GenerateToken(); // A valid token

        // Act
        string invalidToken = token + "Invalid"; // Making it invalid
        bool result = service.VerifyToken(invalidToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyToken_ExpiredToken_ReturnsFalse()
    {
        // Arrange
        DictionaryConfiguration config = new DictionaryConfiguration(new Dictionary<string, string>
            {
                { "SigningKey", "0UrY1tx6Z6GAQKX/xsC1xjQ3uaMHaEs3cRf8kwgEz+Q=" }
            });
        StringLogger<CsrfTokenService> logger = new StringLogger<CsrfTokenService>();
        FixedTimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        CsrfTokenService service = new CsrfTokenService(config, logger, timeProvider);
        string token = service.GenerateToken();

        // Simulate expiration by advancing the clock
        timeProvider.SetTime(timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        // Act
        bool result = service.VerifyToken(token);

        // Assert
        Assert.False(result);
    }

    private static T GetPrivateField<T>(object obj, string fieldName)
    {
        System.Reflection.FieldInfo? field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new ArgumentException($"Field '{fieldName}' not found in type '{obj.GetType()}'");
        return (T)field.GetValue(obj)!;
    }
}