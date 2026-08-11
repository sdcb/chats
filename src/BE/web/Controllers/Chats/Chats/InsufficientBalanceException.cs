using Chats.DB.Enums;

namespace Chats.BE.Controllers.Chats.Chats;

public abstract class ChatServiceException(DBFinishReason errorCode) : Exception
{
    public DBFinishReason ErrorCode => errorCode;

    public override string Message => $"code: {ErrorCode}";
}

public class CustomChatServiceException(DBFinishReason errorCode, string message) : ChatServiceException(errorCode)
{
    public override string Message => message;
}

public class InsufficientBalanceException() : ChatServiceException(DBFinishReason.InsufficientBalance)
{
    public override string Message => "Insufficient balance";
}

public class InvalidModelException(string modelName) : ChatServiceException(DBFinishReason.InvalidModel)
{
    public string ModelName => modelName;

    public override string Message => "The Model does not exist or access is denied.";
}

public class SubscriptionExpiredException(DateTime expiresAt) : ChatServiceException(DBFinishReason.SubscriptionExpired)
{
    public DateTime ExpiresAt => expiresAt;

    public override string Message => "Subscription has expired";
}

public class RawChatServiceException(int statusCode, string body, TimeSpan? retryAfter = null) : ChatServiceException(DBFinishReason.UpstreamError)
{
    public int StatusCode => statusCode;

    public string Body => body;

    public TimeSpan? RetryAfter => retryAfter;

    public override string Message => Body;

    public static async Task<RawChatServiceException> CreateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        TimeSpan? retryAfter = GetRetryAfter(response);
        return new RawChatServiceException((int)response.StatusCode, body, retryAfter);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is DateTimeOffset retryAt)
        {
            TimeSpan delay = retryAt - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }
}
