using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Public.SMSs;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.Security;

public class LoginRateLimiter(ChatsDB db, ILogger<LoginRateLimiter> logger)
{
	private static readonly RateLimitConfig PasswordLimit = new(5, TimeSpan.FromMinutes(10));
	private static readonly RateLimitConfig SmsLimit = new(SmsController.MaxAttempts, TimeSpan.FromSeconds(SmsController.SmsExpirationSeconds));

	public async Task<RateLimitCheckResult> CheckPasswordAsync(int clientInfoId, CancellationToken cancellationToken)
	{
		ClientRateLimitContext clientContext = await GetClientRateLimitContext(clientInfoId, cancellationToken);
		IQueryable<DateTime> query = db.PasswordAttempts
			.AsNoTracking()
			.Where(x => x.ClientInfo.ClientIpId == clientContext.ClientIpId && !x.IsSuccessful)
			.Select(x => x.CreatedAt);

		return await CheckLimitAsync(query, PasswordLimit, "password", "Too many attempts. Please try again later.", clientContext.RateLimitKey, cancellationToken);
	}

	public async Task<RateLimitCheckResult> CheckSmsAsync(int clientInfoId, CancellationToken cancellationToken)
	{
		ClientRateLimitContext clientContext = await GetClientRateLimitContext(clientInfoId, cancellationToken);
		IQueryable<DateTime> query = db.SmsAttempts
			.AsNoTracking()
			.Where(x => x.ClientInfo.ClientIpId == clientContext.ClientIpId
				&& (x.Code != x.SmsRecord.ExpectedCode || x.SmsRecord.CreatedAt.AddSeconds(SmsController.SmsExpirationSeconds) < x.CreatedAt)
				&& x.SmsRecord.StatusId == (byte)DBSmsStatus.WaitingForVerification)
			.Select(x => x.CreatedAt);

		return await CheckLimitAsync(query, SmsLimit, "sms", "Too many attempts.", clientContext.RateLimitKey, cancellationToken);
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

	private async Task<ClientRateLimitContext> GetClientRateLimitContext(int clientInfoId, CancellationToken cancellationToken)
	{
		ClientRateLimitContext? clientContext = await db.ClientInfos
			.AsNoTracking()
			.Where(x => x.Id == clientInfoId)
			.Select(x => new ClientRateLimitContext(
				x.ClientIpId,
				x.ClientIp != null ? x.ClientIp.Ipaddress : null))
			.FirstOrDefaultAsync(cancellationToken);

		return clientContext ?? throw new InvalidOperationException($"ClientInfo {clientInfoId} not found.");
	}

	private readonly record struct RateLimitConfig(int MaxAttempts, TimeSpan Window);

	private readonly record struct ClientRateLimitContext(int ClientIpId, string? IpAddress)
	{
		public string RateLimitKey => IpAddress ?? $"IP#{ClientIpId}";
	}

	public readonly record struct RateLimitCheckResult(bool IsAllowed, string? ErrorMessage, TimeSpan? RetryAfter)
	{
		public static RateLimitCheckResult Allowed() => new(true, null, null);

		public static RateLimitCheckResult Blocked(string errorMessage, TimeSpan? retryAfter) => new(false, errorMessage, retryAfter);
	}
}
