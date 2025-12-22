using Chats.Web.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.Services;

public class ClientInfoManager(IHttpContextAccessor httpContextAccessor, ChatsDB db)
{
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

    public async Task<ClientIp> GetDBClientIP(CancellationToken cancellationToken)
    {
        string ip = ClientIP;
        ClientIp? clientIp = await db.ClientIps.FirstOrDefaultAsync(x => x.Ipaddress == ip, cancellationToken);
        if (clientIp != null)
        {
            return clientIp;
        }

        clientIp = new ClientIp { Ipaddress = ip };
        db.ClientIps.Add(clientIp);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return clientIp;
        }
        catch (DbUpdateException)
        {
            // 唯一约束冲突，说明其他并发请求已经插入成功
            db.Entry(clientIp).State = EntityState.Detached;
            return await db.ClientIps.FirstAsync(x => x.Ipaddress == ip, cancellationToken);
        }
    }

    public async Task<ClientUserAgent> GetDBUserAgent(CancellationToken cancellationToken)
    {
        string userAgent = UserAgent;
        ClientUserAgent? clientUserAgent = await db.ClientUserAgents.FirstOrDefaultAsync(x => x.UserAgent == userAgent, cancellationToken);
        if (clientUserAgent != null)
        {
            return clientUserAgent;
        }

        clientUserAgent = new ClientUserAgent { UserAgent = userAgent };
        db.ClientUserAgents.Add(clientUserAgent);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return clientUserAgent;
        }
        catch (DbUpdateException)
        {
            // 唯一约束冲突，说明其他并发请求已经插入成功
            db.Entry(clientUserAgent).State = EntityState.Detached;
            return await db.ClientUserAgents.FirstAsync(x => x.UserAgent == userAgent, cancellationToken);
        }
    }

    public async Task<ClientInfo> GetClientInfo(CancellationToken cancellationToken)
    {
        ClientIp ip = await GetDBClientIP(cancellationToken);
        ClientUserAgent userAgent = await GetDBUserAgent(cancellationToken);
        ClientInfo? clientInfo = await db.ClientInfos.FirstOrDefaultAsync(x => x.ClientIpId == ip.Id && x.ClientUserAgentId == userAgent.Id, cancellationToken);
        if (clientInfo != null)
        {
            clientInfo.ClientIp = ip;
            clientInfo.ClientUserAgent = userAgent;
            return clientInfo;
        }

        clientInfo = new ClientInfo
        {
            ClientIpId = ip.Id,
            ClientUserAgentId = userAgent.Id,
            ClientIp = ip,
            ClientUserAgent = userAgent,
        };
        db.ClientInfos.Add(clientInfo);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return clientInfo;
        }
        catch (DbUpdateException)
        {
            // 唯一约束冲突，说明其他并发请求已经插入成功
            db.Entry(clientInfo).State = EntityState.Detached;
            clientInfo = await db.ClientInfos.FirstAsync(x => x.ClientIpId == ip.Id && x.ClientUserAgentId == userAgent.Id, cancellationToken);
            clientInfo.ClientIp = ip;
            clientInfo.ClientUserAgent = userAgent;
            return clientInfo;
        }
    }
}

public class AsyncClientInfoManager(IServiceScopeFactory serviceScopeFactory, IHttpContextAccessor httpContextAccessor)
{
    public async Task<int> GetClientInfoId(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ClientInfoManager clientInfoManager = new(httpContextAccessor, scope.ServiceProvider.GetRequiredService<ChatsDB>());
        ClientInfo clientInfo = await clientInfoManager.GetClientInfo(cancellationToken);
        return clientInfo.Id;
    }
}