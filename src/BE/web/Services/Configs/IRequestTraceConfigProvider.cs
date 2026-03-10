namespace Chats.BE.Services.Configs;

public interface IRequestTraceConfigProvider
{
    RequestTraceConfig GetInboundConfig();

    RequestTraceConfig GetOutboundConfig();

    DateTime LastRefreshAtUtc { get; }

    Task RefreshAsync(CancellationToken cancellationToken);

    Task ForceRefreshAsync(CancellationToken cancellationToken);
}
