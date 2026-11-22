using Microsoft.Extensions.Options;

namespace Chats.BE.Services.ProviderFiles;

/// <summary>
/// Periodically marks provider file mappings whose ExpiresAt has passed as expired (status update only).
/// </summary>
public sealed class ProviderFileCleanupService(
    ProviderFileService providerFileService,
    IOptionsMonitor<ProviderFileOptions> options,
    ILogger<ProviderFileCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalSeconds = options.CurrentValue.CleanupIntervalSeconds;
            if (intervalSeconds <= 0)
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                int count = await providerFileService.MarkExpiredAsync(stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("Marked {Count} provider file mapping(s) as expired", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provider file mapping cleanup failed");
            }
        }
    }
}

public static class ProviderFileServiceCollectionExtensions
{
    public static IServiceCollection AddProviderFiles(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProviderFileOptions>(configuration.GetSection(ProviderFileOptions.SectionName));
        services.AddSingleton<IProviderFileClient, OpenAIProviderFileClient>();
        services.AddSingleton<IProviderFileClient, AnthropicProviderFileClient>();
        services.AddSingleton<IProviderFileClientFactory, ProviderFileClientFactory>();
        services.AddSingleton<ProviderFileService>();
        services.AddHostedService<ProviderFileCleanupService>();
        return services;
    }
}
