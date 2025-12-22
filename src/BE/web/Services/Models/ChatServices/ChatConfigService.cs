using Chats.Web.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Services.Models.ChatServices;

public class ChatConfigService(ChatsDB db)
{
    public async Task<ChatConfig> GetOrCreateChatConfig(ChatConfig raw, CancellationToken cancellationToken)
    {
        long hashCode = raw.GenerateDBHashCode();
        ChatConfig? matchingConfig = await db.ChatConfigs
            .Include(x => x.ChatConfigMcps)
            .Where(c => 
                c.ChatConfigArchived!.HashCode == hashCode &&
                c.ModelId == raw.ModelId && 
                c.SystemPrompt == raw.SystemPrompt && 
                c.WebSearchEnabled == raw.WebSearchEnabled && 
                c.ReasoningEffortId == raw.ReasoningEffortId && 
                c.Temperature == raw.Temperature &&
                c.ImageSize == raw.ImageSize && 
                c.CodeExecutionEnabled == raw.CodeExecutionEnabled &&
                c.MaxOutputTokens == raw.MaxOutputTokens &&
                c.ThinkingBudget == raw.ThinkingBudget)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (matchingConfig is not null)
        {
            return matchingConfig;
        }
        else
        {
            ChatConfig newConfig = raw.Clone();
            newConfig.Id = 0;
            newConfig.ChatConfigArchived = new()
            {
                HashCode = hashCode,
            };
            db.ChatConfigs.Add(newConfig);
            await db.SaveChangesAsync(cancellationToken);
            return newConfig;
        }
    }
}
