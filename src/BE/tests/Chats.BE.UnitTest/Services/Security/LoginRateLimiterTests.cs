using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Public.SMSs;
using Chats.BE.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.Services.Security;

public class LoginRateLimiterTests
{
    [Fact]
    public async Task CheckPasswordAsync_AllowsWhenBelowThreshold()
    {
        await using ChatsDB db = CreateDbContext();
        ClientInfo clientInfo = await SeedClientInfoAsync(db);

        db.PasswordAttempts.Add(new PasswordAttempt
        {
            ClientInfoId = clientInfo.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            UserName = "user",
            IsSuccessful = false,
        });
        await db.SaveChangesAsync();

        LoginRateLimiter limiter = new(db, NullLogger<LoginRateLimiter>.Instance);

    LoginRateLimiter.RateLimitCheckResult result = await limiter.CheckPasswordAsync(clientInfo, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckPasswordAsync_BlocksAfterMaxFailures()
    {
        await using ChatsDB db = CreateDbContext();
        ClientInfo clientInfo = await SeedClientInfoAsync(db);

        for (int i = 0; i < 5; i++)
        {
            db.PasswordAttempts.Add(new PasswordAttempt
            {
                ClientInfoId = clientInfo.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                UserName = $"user-{i}",
                IsSuccessful = false,
            });
        }
        await db.SaveChangesAsync();

        LoginRateLimiter limiter = new(db, NullLogger<LoginRateLimiter>.Instance);

    LoginRateLimiter.RateLimitCheckResult result = await limiter.CheckPasswordAsync(clientInfo, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("Too many attempts. Please try again later.", result.ErrorMessage);
        Assert.True(result.RetryAfter.HasValue);
        Assert.True(result.RetryAfter.Value >= TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckPasswordAsync_AggregatesAttemptsByIp()
    {
        await using ChatsDB db = CreateDbContext();
        ClientInfo firstClientInfo = await SeedClientInfoAsync(db);
        ClientInfo secondClientInfo = await SeedClientInfoWithExistingIpAsync(db, firstClientInfo.ClientIpId, "test-agent-2");

        for (int i = 0; i < 5; i++)
        {
            db.PasswordAttempts.Add(new PasswordAttempt
            {
                ClientInfoId = firstClientInfo.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                IsSuccessful = false,
                UserName = $"user-{i}",
            });
        }
        await db.SaveChangesAsync();

        LoginRateLimiter limiter = new(db, NullLogger<LoginRateLimiter>.Instance);

    LoginRateLimiter.RateLimitCheckResult result = await limiter.CheckPasswordAsync(secondClientInfo, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("Too many attempts. Please try again later.", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckSmsAsync_IgnoresSuccessfulAttempts()
    {
        await using ChatsDB db = CreateDbContext();
        ClientInfo clientInfo = await SeedClientInfoAsync(db);
        DateTime baseTime = DateTime.UtcNow;
        SmsRecord record = await SeedSmsRecordAsync(db, baseTime);

        // Four successful attempts that should be ignored and one failure inside the window
        for (int i = 0; i < 4; i++)
        {
            db.SmsAttempts.Add(new SmsAttempt
            {
                SmsRecordId = record.Id,
                ClientInfoId = clientInfo.Id,
                CreatedAt = baseTime.AddSeconds(30 + i),
                Code = "123456",
            });
        }

        db.SmsAttempts.Add(new SmsAttempt
        {
            SmsRecordId = record.Id,
            ClientInfoId = clientInfo.Id,
            CreatedAt = baseTime.AddSeconds(120),
            Code = "000000",
        });
        await db.SaveChangesAsync();

        LoginRateLimiter limiter = new(db, NullLogger<LoginRateLimiter>.Instance);

    LoginRateLimiter.RateLimitCheckResult result = await limiter.CheckSmsAsync(clientInfo, CancellationToken.None);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckSmsAsync_BlocksAfterThreeFailuresWithinWindow()
    {
        await using ChatsDB db = CreateDbContext();
        ClientInfo clientInfo = await SeedClientInfoAsync(db);
        DateTime baseTime = DateTime.UtcNow;
        SmsRecord record = await SeedSmsRecordAsync(db, baseTime);

        for (int i = 0; i < SmsController.MaxAttempts; i++)
        {
            db.SmsAttempts.Add(new SmsAttempt
            {
                SmsRecordId = record.Id,
                ClientInfoId = clientInfo.Id,
                CreatedAt = baseTime.AddSeconds(i * 30),
                Code = "000000",
            });
        }
        await db.SaveChangesAsync();

        LoginRateLimiter limiter = new(db, NullLogger<LoginRateLimiter>.Instance);

    LoginRateLimiter.RateLimitCheckResult result = await limiter.CheckSmsAsync(clientInfo, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("Too many attempts.", result.ErrorMessage);
        Assert.True(result.RetryAfter.HasValue);
        Assert.True(result.RetryAfter.Value >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RecordPasswordAttemptAsync_PersistsAttempt()
    {
        await using ChatsDB db = CreateDbContext();
        ClientInfo clientInfo = await SeedClientInfoAsync(db);

        LoginRateLimiter limiter = new(db, NullLogger<LoginRateLimiter>.Instance);

        await limiter.RecordPasswordAttemptAsync("demo", clientInfo.Id, null, true, null, CancellationToken.None);

        PasswordAttempt attempt = await db.PasswordAttempts.AsNoTracking().SingleAsync();
        Assert.Equal("demo", attempt.UserName);
        Assert.True(attempt.IsSuccessful);
        Assert.Equal(clientInfo.Id, attempt.ClientInfoId);
    }

    private static ChatsDB CreateDbContext()
    {
        DbContextOptions<ChatsDB> options = new DbContextOptionsBuilder<ChatsDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ChatsDB(options);
    }

    private static async Task<ClientInfo> SeedClientInfoAsync(ChatsDB db)
    {
        ClientIp ip = new() { Ipaddress = "127.0.0.1" };
        ClientUserAgent ua = new() { UserAgent = "test-agent" };
        db.ClientIps.Add(ip);
        db.ClientUserAgents.Add(ua);
        await db.SaveChangesAsync();

        ClientInfo info = new()
        {
            ClientIpId = ip.Id,
            ClientUserAgentId = ua.Id,
            ClientIp = ip,
            ClientUserAgent = ua,
        };
        db.ClientInfos.Add(info);
        await db.SaveChangesAsync();
        return info;
    }

    private static async Task<SmsRecord> SeedSmsRecordAsync(ChatsDB db, DateTime? createdAt = null)
    {
        SmsRecord record = new()
        {
            PhoneNumber = "1234567890",
            TypeId = 1,
            StatusId = (byte)DBSmsStatus.WaitingForVerification,
            ExpectedCode = "123456",
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
        db.SmsRecords.Add(record);
        await db.SaveChangesAsync();
        return record;
    }

    private static async Task<ClientInfo> SeedClientInfoWithExistingIpAsync(ChatsDB db, int clientIpId, string userAgent)
    {
        ClientUserAgent ua = new() { UserAgent = userAgent };
        db.ClientUserAgents.Add(ua);
        await db.SaveChangesAsync();

        ClientIp ip = await db.ClientIps.FindAsync(clientIpId) ?? throw new InvalidOperationException("ClientIp not found");

        ClientInfo info = new()
        {
            ClientIpId = clientIpId,
            ClientUserAgentId = ua.Id,
            ClientIp = ip,
            ClientUserAgent = ua,
        };
        db.ClientInfos.Add(info);
        await db.SaveChangesAsync();

        return info;
    }
}
