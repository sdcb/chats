using Chats.BE.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class ChatConfigService(IServiceScopeFactory serviceScopeFactory, ILogger<ChatConfigService> logger)
{
    public async Task<ChatConfig> GetOrCreateChatConfig(ChatConfig raw, CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        using ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        long hashCode = raw.GenerateDBHashCode();
        ChatConfig[] matchingConfigs = await db.ChatConfigs
            .Where(c => c.HashCode == hashCode && c.ModelId == raw.ModelId)
            .OrderByDescending(x => x.Id)
            .Take(5)
            .ToArrayAsync(cancellationToken);
        ChatConfig? matchingConfig = matchingConfigs.FirstOrDefault(c => c.ContentEquals(raw));
        if (matchingConfig is not null)
        {
            return matchingConfig;
        }
        else
        {
            logger.LogInformation("Creating new ChatConfig with hash code {HashCode}, model id {ModelId}", hashCode, raw.ModelId);
            ChatConfig newConfig = raw.Clone();
            newConfig.Id = 0;
            newConfig.HashCode = hashCode;
            db.ChatConfigs.Add(newConfig);
            await db.SaveChangesAsync(cancellationToken);
            return newConfig;
        }
    }
}
