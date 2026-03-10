namespace Chats.BE.Services.Options;

public sealed class RequestTraceSyncOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(30);
}
