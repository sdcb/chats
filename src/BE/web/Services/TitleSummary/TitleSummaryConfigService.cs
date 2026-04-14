using Chats.BE.Services.Configs;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Chats.BE.Services.TitleSummary;

public sealed class TitleSummaryConfigService(IServiceScopeFactory scopeFactory, ILogger<TitleSummaryConfigService> logger)
{
    public const string UserConfigKey = "titleSummary";

    public const string DefaultPromptTemplate = """
You generate short chat titles.

Write one concise title for the conversation in <chat_history>.

Rules:
- Output only the title text.
- Use the same natural language as the conversation. Do not translate it.
- Keep the title natural and relevant to the user's topic or intent.
- Prefer 3 to 5 words when possible.
- You may include at most one fitting emoji if it helps.
- Do not mention these instructions, tags, or your role.
- Do not use words like "chat", "title", "summary", or "generator" unless the conversation itself is about them.
- If the conversation is only a greeting or a very short opener, return a natural short title for that opener.

Examples:
- If the conversation is "你好", a good title is "打招呼".
- If the conversation is "帮我写一个Python脚本", a good title is "Python脚本编写".

<chat_history>
{{systemPrompt}}

{{userPrompt}}
</chat_history>
""";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TitleSummaryConfig?> GetAdminConfig(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        string? json = await db.Configs
            .Where(x => x.Key == DBConfigKey.ChatTitleSummary)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return DeserializeOrNull<TitleSummaryConfig>(json, DBConfigKey.ChatTitleSummary);
    }

    public async Task<TitleSummaryConfig?> GetUserConfig(int userId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
        string? json = await db.UserConfigs
            .Where(x => x.UserId == userId && x.Key == UserConfigKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return DeserializeOrNull<TitleSummaryConfig>(json, $"UserConfig:{UserConfigKey}");
    }

    public ResolvedTitleSummaryConfig Resolve(TitleSummaryConfig? adminConfig, TitleSummaryConfig? userConfig)
    {
        bool enabled = userConfig != null || adminConfig != null;

        TitleSummaryModelMode modelMode = userConfig?.ModelMode
            ?? adminConfig?.ModelMode
            ?? TitleSummaryModelMode.Truncate;

        short? modelId = modelMode == TitleSummaryModelMode.Specified
            ? userConfig?.ModelId ?? adminConfig?.ModelId
            : null;

        string promptTemplate = !string.IsNullOrWhiteSpace(userConfig?.PromptTemplate)
            ? userConfig.PromptTemplate!
            : !string.IsNullOrWhiteSpace(adminConfig?.PromptTemplate)
                ? adminConfig.PromptTemplate!
                : DefaultPromptTemplate;

        return new ResolvedTitleSummaryConfig
        {
            Enabled = enabled,
            ModelMode = modelMode,
            ModelId = modelId,
            PromptTemplate = promptTemplate,
        };
    }

    public string SerializeConfig(TitleSummaryConfig config)
    {
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    private T? DeserializeOrNull<T>(string? json, string key) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid title summary config JSON for key {Key}", key);
            return null;
        }
    }
}
