using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class ClientInfoManager(IHttpContextAccessor httpContextAccessor, IServiceScopeFactory serviceScopeFactory)
{
    private readonly Lock clientInfoIdLock = new();
    private Task<int>? clientInfoIdTask;

    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available.");

    public string ClientIP
    {
        get
        {
            HttpContext context = HttpContext;
            return context!.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                   context!.Connection.RemoteIpAddress!.ToString();
        }
    }

    public string UserAgent => HttpContext.Request.Headers.UserAgent.ToString();

    public Task<int> GetClientInfoId(CancellationToken cancellationToken = default)
    {
        if (clientInfoIdTask != null)
        {
            return clientInfoIdTask;
        }

        lock (clientInfoIdLock)
        {
            clientInfoIdTask ??= GetClientInfoIdCore(cancellationToken);
            return clientInfoIdTask;
        }
    }

    private async Task<int> GetClientInfoIdCore(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        string ip = ClientIP;
        string userAgent = UserAgent;

        int? existingClientInfoId = await db.ClientInfos
            .AsNoTracking()
            .Where(x => x.ClientIp.Ipaddress == ip && x.ClientUserAgent.UserAgent == userAgent)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingClientInfoId.HasValue)
        {
            return existingClientInfoId.Value;
        }

        int? clientIpId = await db.ClientIps
            .AsNoTracking()
            .Where(x => x.Ipaddress == ip)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!clientIpId.HasValue)
        {
            ClientIp clientIp = new() { Ipaddress = ip };
            db.ClientIps.Add(clientIp);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                clientIpId = clientIp.Id;
            }
            catch (DbUpdateException)
            {
                db.Entry(clientIp).State = EntityState.Detached;
                clientIpId = await db.ClientIps
                    .AsNoTracking()
                    .Where(x => x.Ipaddress == ip)
                    .Select(x => (int?)x.Id)
                    .FirstAsync(cancellationToken);
            }
        }

        int? clientUserAgentId = await db.ClientUserAgents
            .AsNoTracking()
            .Where(x => x.UserAgent == userAgent)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!clientUserAgentId.HasValue)
        {
            ClientUserAgent clientUserAgent = new() { UserAgent = userAgent };
            db.ClientUserAgents.Add(clientUserAgent);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                clientUserAgentId = clientUserAgent.Id;
            }
            catch (DbUpdateException)
            {
                db.Entry(clientUserAgent).State = EntityState.Detached;
                clientUserAgentId = await db.ClientUserAgents
                    .AsNoTracking()
                    .Where(x => x.UserAgent == userAgent)
                    .Select(x => (int?)x.Id)
                    .FirstAsync(cancellationToken);
            }
        }

        ClientInfo clientInfo = new()
        {
            ClientIpId = clientIpId.Value,
            ClientUserAgentId = clientUserAgentId.Value,
        };
        db.ClientInfos.Add(clientInfo);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return clientInfo.Id;
        }
        catch (DbUpdateException)
        {
            db.Entry(clientInfo).State = EntityState.Detached;
            return await db.ClientInfos
                .AsNoTracking()
                .Where(x => x.ClientIpId == clientIpId.Value && x.ClientUserAgentId == clientUserAgentId.Value)
                .Select(x => x.Id)
                .FirstAsync(cancellationToken);
        }
    }
}