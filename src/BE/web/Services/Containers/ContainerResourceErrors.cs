namespace Chats.BE.Services.Containers;

public static class ContainerResourceErrorCodes
{
    public const string QuotaExceeded = "QuotaExceeded";
    public const string ContainerStopped = "ContainerStopped";
    public const string ContainerDeleted = "ContainerDeleted";
    public const string RuntimeNodeNotImplemented = "RuntimeNodeNotImplemented";
    public const string RuntimeNodeUnavailable = "RuntimeNodeUnavailable";
    public const string ImageNotAllowed = "ImageNotAllowed";
    public const string NetworkModeNotAllowed = "NetworkModeNotAllowed";
    public const string BackendOperationFailed = "BackendOperationFailed";
    public const string TemplateConfigurationWarning = "TemplateConfigurationWarning";
    public const string InvalidConfiguration = "InvalidConfiguration";
    public const string NetworkUpdateNotSupported = "NetworkUpdateNotSupported";
}

public sealed class ContainerResourceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
