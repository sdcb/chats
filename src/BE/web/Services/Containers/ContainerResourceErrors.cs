namespace Chats.BE.Services.Containers;

public static class ContainerResourceErrorCodes
{
    public const string QuotaExceeded = "QuotaExceeded";
    public const string ContainerNotFound = "ContainerNotFound";
    public const string NotFound = "NotFound";
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
    public const string PermanentContainerRequired = "PermanentContainerRequired";
    public const string PortPoolNotConfigured = "PortPoolNotConfigured";
    public const string PortPoolExhausted = "PortPoolExhausted";
    public const string InvalidPortMapping = "InvalidPortMapping";
    public const string PortMappingNotSupported = "PortMappingNotSupported";
    public const string InvalidMountPath = "InvalidMountPath";
    public const string MountPathConflict = "MountPathConflict";
    public const string RuntimeNodeMismatch = "RuntimeNodeMismatch";
    public const string VolumeInUse = "VolumeInUse";
}

public sealed class ContainerResourceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
