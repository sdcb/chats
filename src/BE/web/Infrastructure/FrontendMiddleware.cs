using Microsoft.Extensions.FileProviders;

namespace Chats.BE.Infrastructure;

public class FrontendMiddleware(RequestDelegate next, IWebHostEnvironment webHostEnvironment)
{
    private readonly IFileProvider fileProvider = webHostEnvironment.WebRootFileProvider;

    public async Task Invoke(HttpContext context)
    {
        do
        {
            if (ShouldBypassProcessing(context))
            {
                break;
            }

            foreach (string tryPath in EnumerateTryPathes(context.Request.Path))
            {
                IFileInfo fileInfo = fileProvider.GetFileInfo(tryPath);
                if (fileInfo.Exists)
                {
                    context.Request.Path = tryPath;
                    break;
                }
            }
        } while (false);

        await next(context);
    }

    static IEnumerable<string> EnumerateTryPathes(string requestPath)
    {
        if (requestPath.EndsWith('/'))
        {
            yield return requestPath + "index.html"; // example: /login -> /login/index.html
        }

        if (!Path.HasExtension(requestPath))
        {
            yield return requestPath + ".html"; // example: /login -> /login.html

            int lastIndexOfSlash = requestPath.LastIndexOf('/');
            if (lastIndexOfSlash != -1)
            {
                string prefixPart = requestPath[..lastIndexOfSlash];
                yield return prefixPart + ".html"; // example: /login/ -> /login.html
                yield return prefixPart + "/[id].html";
            }
        }
    }

    private bool ShouldBypassProcessing(HttpContext context)
    {
        return context.Request.Path.Value == null ||
            context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/v1") ||
            !IsGetOrHeadMethod(context.Request.Method) ||
            fileProvider.GetFileInfo(context.Request.Path).Exists;
    }

    private static bool IsGetOrHeadMethod(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

    public static void ApplyCacheHeaders(HttpContext context)
    {
        PathString path = context.Request.Path;
        if (path.StartsWithSegments("/_next/static"))
        {
            // Content-hashed build output never changes for a given URL.
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (path.Value is string value &&
            (value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("/sw.js", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("/manifest.json", StringComparison.OrdinalIgnoreCase)))
        {
            // Must be revalidated on every load so new deployments (and service worker updates) are picked up.
            context.Response.Headers.CacheControl = "no-cache";
        }
    }
}
