using Chats.Web.DB;
using Chats.Web.DB.Jsons;
using Chats.Web.Services.Common;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Chats.Web.Services.Configs;

public class GlobalDBConfig(ChatsDB db, ILogger<GlobalDBConfig> logger)
{
    public async Task<JsonKeycloakConfig?> GetKeycloakConfig(CancellationToken cancellationToken)
    {
        LoginService? loginService = await db.LoginServices.SingleOrDefaultAsync(s => s.Type == KnownLoginProviders.Keycloak, cancellationToken);
        if (loginService == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonKeycloakConfig>(loginService.Configs);
    }

    public Task<TencentSmsConfig> GetTencentSmsConfig(CancellationToken cancellationToken) => GetRequiredConfigByKey<TencentSmsConfig>(DBConfigKey.TencentSms, cancellationToken);

    public Task<SiteInfo?> GetFillingInfo(CancellationToken cancellationToken) => GetConfigByKey<SiteInfo>(DBConfigKey.SiteInfo, cancellationToken);

    private async Task<T?> GetConfigByKey<T>(string key, CancellationToken cancellationToken) where T : class
    {
        string? configText = await db.Configs
            .Where(s => s.Key == key)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (configText == null) return null;

        T? config = JsonSerializer.Deserialize<T>(configText);
        if (config == null)
        {
            logger.LogError("key: {key} is invalid json: {configText}", key, configText);
            throw new Exception($"{key} is invalid json.");
        }

        return config;
    }

    private async Task<T> GetRequiredConfigByKey<T>(string key, CancellationToken cancellationToken)
    {
        string? configText = await db.Configs
            .Where(s => s.Key == key)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new Exception($"{key} not found");

        T? config = JsonSerializer.Deserialize<T>(configText);
        if (config == null)
        {
            logger.LogError("key: {key} is invalid json: {configText}", key, configText);
            throw new Exception($"{key} is invalid json.");
        }

        return config;
    }
}
