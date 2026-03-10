namespace Chats.BE.Services.Options;

public sealed class RequestTraceQueueOptions
{
    public int Capacity { get; init; } = 1000;
}