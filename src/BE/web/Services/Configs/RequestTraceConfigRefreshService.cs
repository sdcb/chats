using Chats.BE.Services.Options;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.Configs;

public sealed class RequestTraceConfigRefreshService(
    IRequestTraceConfigProvider provider,
    IOptions<RequestTraceSyncOptions> options,
    ILogger<RequestTraceConfigRefreshService> logger) : BackgroundService
{
    private readonly TimeSpan _pollInterval = options.Value.PollInterval <= TimeSpan.Zero ? TimeSpan.FromMinutes(30) : options.Value.PollInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await provider.ForceRefreshAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Request trace config warmup refresh failed.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
                await provider.ForceRefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Request trace config refresh loop failed.");
            }
        }
    }
}
