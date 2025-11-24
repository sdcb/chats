using Chats.BE.DB.Enums;

namespace Chats.BE.DB;

/// <summary>
/// 静态的模型提供者信息，替代数据库中的 ModelProvider 表
/// </summary>
public static class ModelProviderInfo
{
    private record ProviderInfo(
        DBModelProvider Id,
        string Name,
        string? InitialHost,
        string? InitialSecret
    );

    private static readonly Dictionary<DBModelProvider, ProviderInfo> _providers = new()
    {
        [DBModelProvider.Test] = new(
            DBModelProvider.Test,
            "Test",
            null,
            null
        ),
        [DBModelProvider.AzureAIFoundry] = new(
            DBModelProvider.AzureAIFoundry,
            "Azure AI Foundry",
            "https://yourresource.openai.azure.com/openai/v1/",
            ""
        ),
        [DBModelProvider.HunYuan] = new(
            DBModelProvider.HunYuan,
            "Tencent Hunyuan",
            "https://api.hunyuan.cloud.tencent.com/v1",
            "sk-"
        ),
        [DBModelProvider.LingYi] = new(
            DBModelProvider.LingYi,
            "01.ai",
            "https://api.lingyiwanwu.com/v1",
            ""
        ),
        [DBModelProvider.Moonshot] = new(
            DBModelProvider.Moonshot,
            "Moonshot",
            "https://api.moonshot.cn/v1",
            ""
        ),
        [DBModelProvider.OpenAI] = new(
            DBModelProvider.OpenAI,
            "OpenAI",
            "https://api.openai.com/v1",
            ""
        ),
        [DBModelProvider.WenXinQianFan] = new(
            DBModelProvider.WenXinQianFan,
            "Wenxin Qianfan",
            "https://qianfan.baidubce.com/v2",
            """{"appId": "app-***", "apiKey":"bce-v3/***"}"""
        ),
        [DBModelProvider.AliyunDashscope] = new(
            DBModelProvider.AliyunDashscope,
            "DashScope",
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "sk-***"
        ),
        [DBModelProvider.Sparkdesk] = new(
            DBModelProvider.Sparkdesk,
            "Xunfei SparkDesk",
            "https://spark-api-open.xf-yun.com/v1",
            ""
        ),
        [DBModelProvider.ZhiPuAI] = new(
            DBModelProvider.ZhiPuAI,
            "Zhipu AI",
            "https://open.bigmodel.cn/api/paas/v4/",
            ""
        ),
        [DBModelProvider.DeepSeek] = new(
            DBModelProvider.DeepSeek,
            "DeepSeek",
            "https://api.deepseek.com/v1",
            ""
        ),
        [DBModelProvider.xAI] = new(
            DBModelProvider.xAI,
            "x.ai",
            "https://api.x.ai/v1",
            "xai-yourkey"
        ),
        [DBModelProvider.GithubModels] = new(
            DBModelProvider.GithubModels,
            "Github Models",
            "https://models.github.ai/inference",
            "ghp_yourkey"
        ),
        [DBModelProvider.GoogleAI] = new(
            DBModelProvider.GoogleAI,
            "Google AI",
            "https://generativelanguage.googleapis.com/v1beta/openai/",
            ""
        ),
        [DBModelProvider.Ollama] = new(
            DBModelProvider.Ollama,
            "Ollama",
            "http://localhost:11434/v1",
            "ollama"
        ),
        [DBModelProvider.MiniMax] = new(
            DBModelProvider.MiniMax,
            "MiniMax",
            "https://api.minimax.chat/v1",
            "your-key"
        ),
        [DBModelProvider.Doubao] = new(
            DBModelProvider.Doubao,
            "Doubao",
            "https://ark.cn-beijing.volces.com/api/v3/",
            "your-key"
        ),
        [DBModelProvider.SiliconFlow] = new(
            DBModelProvider.SiliconFlow,
            "SiliconFlow",
            "https://api.siliconflow.cn/v1",
            "sk-yourkey"
        ),
        [DBModelProvider.OpenRouter] = new(
            DBModelProvider.OpenRouter,
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "sk-or-v1-***"
        ),
        [DBModelProvider.TokenPony] = new(
            DBModelProvider.TokenPony,
            "Token Pony",
            "https://api.tokenpony.cn/v1",
            "sk-"
        ),
    };

    private static readonly Dictionary<string, DBModelProvider> _nameToIdMap = _providers
        .ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    public static string GetName(DBModelProvider providerId)
    {
        return _providers.TryGetValue(providerId, out ProviderInfo? info) ? info.Name : providerId.ToString();
    }

    public static string? GetInitialHost(DBModelProvider providerId)
    {
        return _providers.TryGetValue(providerId, out ProviderInfo? info) ? info.InitialHost : null;
    }

    public static string? GetInitialSecret(DBModelProvider providerId)
    {
        return _providers.TryGetValue(providerId, out ProviderInfo? info) ? info.InitialSecret : null;
    }

    public static DBModelProvider? GetIdByName(string name)
    {
        return _nameToIdMap.TryGetValue(name, out DBModelProvider id) ? id : null;
    }

    public static bool TryGetIdByName(string name, out DBModelProvider providerId)
    {
        return _nameToIdMap.TryGetValue(name, out providerId);
    }

    public static IEnumerable<DBModelProvider> GetAllProviderIds()
    {
        return _providers.Keys;
    }

    public static bool IsValidProviderId(DBModelProvider providerId)
    {
        return _providers.ContainsKey(providerId);
    }
}
