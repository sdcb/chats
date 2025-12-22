namespace Chats.Web.Services.Models;

public enum DBFinishReason : byte
{
    Success = 0,
    Stop = 1,
    Length = 2,
    ToolCalls = 3,
    ContentFilter = 4,
    FunctionCall = 5,
    UnknownError = 100,
    InsufficientBalance = 101,
    UpstreamError = 102,
    InvalidModel = 103,
    SubscriptionExpired = 104,
    BadParameter = 105,
    Cancelled = 106,
    InternalConfigIssue = 107,
}
