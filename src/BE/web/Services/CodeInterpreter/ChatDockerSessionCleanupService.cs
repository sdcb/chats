using Chats.DB;
using Chats.DockerInterface;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services.CodeInterpreter;

public sealed class ChatDockerSessionCleanupService(
    IServiceScopeFactory scopeFactory,
    IDockerService dockerService,
    ILogger<ChatDockerSessionCleanupService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IDockerService _dockerService = dockerService;
    private readonly ILogger<ChatDockerSessionCleanupService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Best-effort cleanup loop.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatDockerSession cleanup loop failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CleanupOnce(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        DateTime now = DateTime.UtcNow;
        List<ChatDockerSession> expired = await db.ChatDockerSessions
            .Where(x => x.TerminatedAt == null && x.ExpiresAt < now)
            .OrderBy(x => x.ExpiresAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return;

        foreach (ChatDockerSession session in expired)
        {
            try
            {
                await _dockerService.DeleteContainerAsync(session.ContainerId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete expired container {containerId}", session.ContainerId);
            }

            session.TerminatedAt = now;
            session.LastActiveAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
