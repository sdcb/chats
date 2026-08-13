namespace Chats.BE.Services.Options;

public sealed class ChatOptions
{
    public int? MaxTransientRetries { get; init; }

    [Obsolete("Use MaxTransientRetries. Retry429Times will be removed in Chats 1.15.")]
    public int? Retry429Times { get; init; }
}
