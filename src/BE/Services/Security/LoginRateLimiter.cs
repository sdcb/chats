using Chats.BE.Controllers.Public.SMSs;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chats.BE.Services.Security;

public class LoginRateLimiter(ChatsDB db, ILogger<LoginRateLimiter> logger)
{
	private static readonly RateLimitConfig PasswordLimit = new(5, TimeSpan.FromMinutes(10));
	private static readonly RateLimitConfig SmsLimit = new(SmsController.MaxAttempts, TimeSpan.FromSeconds(SmsController.SmsExpirationSeconds));

	public async Task<RateLimitCheckResult> CheckPasswordAsync(ClientInfo clientInfo, CancellationToken cancellationToken)
	{
		IQueryable<DateTime> query = db.PasswordAttempts
			.AsNoTracking()
			.Where(x => x.ClientInfo.ClientIpId == clientInfo.ClientIpId && !x.IsSuccessful)
			.Select(x => x.CreatedAt);

		return await CheckLimitAsync(query, PasswordLimit, "password", "Too many attempts. Please try again later.", GetRateLimitKey(clientInfo), cancellationToken);
	}

	public async Task<RateLimitCheckResult> CheckSmsAsync(ClientInfo clientInfo, CancellationToken cancellationToken)
	{
		IQueryable<DateTime> query = db.SmsAttempts
			.AsNoTracking()
			.Where(x => x.ClientInfo.ClientIpId == clientInfo.ClientIpId
				&& (x.Code != x.SmsRecord.ExpectedCode || x.SmsRecord.CreatedAt.AddSeconds(SmsController.SmsExpirationSeconds) < x.CreatedAt)
				&& x.SmsRecord.StatusId == (byte)DBSmsStatus.WaitingForVerification)
			.Select(x => x.CreatedAt);

		return await CheckLimitAsync(query, SmsLimit, "sms", "Too many attempts.", GetRateLimitKey(clientInfo), cancellationToken);
	}

	public async Task RecordPasswordAttemptAsync(string userName, int clientInfoId, int? userId, bool isSuccessful, string? failureReason, CancellationToken cancellationToken)
	{
		PasswordAttempt attempt = new()
		{
			UserName = userName,
			ClientInfoId = clientInfoId,
			UserId = userId,
			IsSuccessful = isSuccessful,
			FailureReason = failureReason,
			CreatedAt = DateTime.UtcNow,
		};

		db.PasswordAttempts.Add(attempt);
		await db.SaveChangesAsync(cancellationToken);
	}

	public async Task RecordKeycloakAttemptAsync(string provider, int clientInfoId, int? userId, bool isSuccessful, string? sub, string? email, string? failureReason, CancellationToken cancellationToken)
	{
		KeycloakAttempt attempt = new()
		{
			Provider = provider,
			ClientInfoId = clientInfoId,
			UserId = userId,
			IsSuccessful = isSuccessful,
			Sub = sub,
			Email = email,
			FailureReason = failureReason,
			CreatedAt = DateTime.UtcNow,
		};

		db.KeycloakAttempts.Add(attempt);
		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task<RateLimitCheckResult> CheckLimitAsync(
		IQueryable<DateTime> attemptQuery,
		RateLimitConfig config,
		string logContext,
		string errorMessage,
		string rateLimitKey,
		CancellationToken cancellationToken)
	{
		DateTime utcNow = DateTime.UtcNow;
		DateTime windowStart = utcNow - config.Window;

		IQueryable<DateTime> windowQuery = attemptQuery.Where(createdAt => createdAt >= windowStart);
		int attemptCount = await windowQuery.CountAsync(cancellationToken);
		if (attemptCount >= config.MaxAttempts)
		{
			DateTime oldest = await windowQuery
				.OrderBy(x => x)
				.FirstAsync(cancellationToken);

			TimeSpan retryAfter = oldest + config.Window - utcNow;
			if (retryAfter < TimeSpan.Zero)
			{
				retryAfter = TimeSpan.Zero;
			}

			logger.LogWarning("Rate limit triggered for {Context}. Key: {RateLimitKey}, Attempts: {Attempts}, WindowMinutes: {WindowMinutes}, RetryAfterSeconds: {RetryAfterSeconds}",
				logContext,
				rateLimitKey,
				attemptCount,
				config.Window.TotalMinutes,
				Math.Round(retryAfter.TotalSeconds));

			return RateLimitCheckResult.Blocked(errorMessage, retryAfter);
		}

		return RateLimitCheckResult.Allowed();
	}

	private static string GetRateLimitKey(ClientInfo clientInfo)
	{
		return clientInfo.ClientIp?.Ipaddress ?? $"IP#{clientInfo.ClientIpId}";
	}

	private readonly record struct RateLimitConfig(int MaxAttempts, TimeSpan Window);

	public readonly record struct RateLimitCheckResult(bool IsAllowed, string? ErrorMessage, TimeSpan? RetryAfter)
	{
		public static RateLimitCheckResult Allowed() => new(true, null, null);

		public static RateLimitCheckResult Blocked(string errorMessage, TimeSpan? retryAfter) => new(false, errorMessage, retryAfter);
	}
}
