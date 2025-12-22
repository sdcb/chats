using Chats.Web.Services.Common;
using Microsoft.Extensions.Primitives;

namespace Chats.Web.Services;

public class HostUrlService(IHttpContextAccessor _ctx)
{
    public virtual string GetBEUrl()
    {
        HttpRequest request = _ctx.HttpContext!.Request;
        IHeaderDictionary headers = request.Headers;

        string scheme = headers.TryGetValue("X-Forwarded-Proto", out StringValues schemeValue) ? schemeValue.FirstOrDefault()! : request.Scheme;
        string host = headers.TryGetValue("X-Forwarded-Host", out StringValues hostValue) ? hostValue.FirstOrDefault()! : request.Host.ToString();

        string url = $"{scheme}://{host}";

        return url;
    }

    public virtual string GetFEUrl()
    {
        HttpRequest request = _ctx.HttpContext!.Request;
        IHeaderDictionary headers = request.Headers;
        string origin = headers.TryGetValue("Origin", out StringValues originValue) ? originValue.FirstOrDefault()! : throw new InvalidOperationException("Origin header not found");
        return origin;
    }

    public virtual string GetKeycloakSsoRedirectUrl()
    {
        return $"{GetFEUrl()}/authorizing?provider={KnownLoginProviders.Keycloak}";
    }
}
