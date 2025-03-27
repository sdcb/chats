﻿using Chats.BE.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class ChatConfigService(ChatsDB db, ILogger<ChatConfigService> logger)
{
    public async Task<ChatConfig> GetOrCreateChatConfig(ChatConfig raw, CancellationToken cancellationToken)
    {
        long hashCode = raw.GenerateDBHashCode();
        ChatConfig? matchingConfig = await db.ChatConfigs
            .Where(c => 
                c.HashCode == hashCode && 
                c.ModelId == raw.ModelId && 
                c.SystemPrompt == raw.SystemPrompt && 
                c.WebSearchEnabled == raw.WebSearchEnabled && 
                c.ReasoningEffort == raw.ReasoningEffort && 
                c.Temperature == raw.Temperature)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
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
