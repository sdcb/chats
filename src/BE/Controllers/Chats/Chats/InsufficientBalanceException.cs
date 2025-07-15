using Chats.BE.Services.Models;

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