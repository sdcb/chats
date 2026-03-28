using Android.Content.Res;

namespace Chats.Mobile;

internal sealed class AssetFileResolver
{
    private readonly AssetManager _assetManager;
    private readonly Lazy<HashSet<string>> _assetPaths;

    public AssetFileResolver(AssetManager assetManager)
    {
        _assetManager = assetManager;
        _assetPaths = new Lazy<HashSet<string>>(BuildAssetIndex, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool TryResolveAssetPath(string requestPath, string? requestMethod, out string assetPath)
    {
        assetPath = string.Empty;
        string normalizedRequestPath = NormalizeRequestPath(requestPath);

        if (AssetExists(normalizedRequestPath))
        {
            assetPath = normalizedRequestPath.TrimStart('/');
            return true;
        }

        if (ShouldBypassProcessing(normalizedRequestPath, requestMethod))
        {
            return false;
        }

        foreach (string candidate in EnumerateTryPaths(normalizedRequestPath))
        {
            if (AssetExists(candidate))
            {
                assetPath = candidate.TrimStart('/');
                return true;
            }
        }

        return false;
    }

    public Stream OpenRead(string assetPath)
    {
        return _assetManager.Open(assetPath);
    }

    private bool ShouldBypassProcessing(string requestPath, string? requestMethod)
    {
        return string.IsNullOrWhiteSpace(requestPath) ||
            requestPath.StartsWith("/wwwroot/api", StringComparison.OrdinalIgnoreCase) ||
            requestPath.StartsWith("/wwwroot/swagger", StringComparison.OrdinalIgnoreCase) ||
            requestPath.StartsWith("/wwwroot/v1", StringComparison.OrdinalIgnoreCase) ||
            !IsGetOrHeadMethod(requestMethod) ||
            AssetExists(requestPath);
    }

    private bool AssetExists(string requestPath)
    {
        string assetPath = NormalizeRequestPath(requestPath).TrimStart('/');
        return _assetPaths.Value.Contains(assetPath);
    }

    private HashSet<string> BuildAssetIndex()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        IndexDirectory("wwwroot", paths);
        return paths;
    }

    private void IndexDirectory(string currentDirectory, HashSet<string> paths)
    {
        string[] children = _assetManager.List(currentDirectory) ?? [];
        foreach (string child in children)
        {
            string assetPath = $"{currentDirectory}/{child}";
            string[] nested = _assetManager.List(assetPath) ?? [];
            if (nested.Length > 0)
            {
                IndexDirectory(assetPath, paths);
                continue;
            }

            paths.Add(assetPath);
        }
    }

    private static string NormalizeRequestPath(string requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return "/wwwroot/";
        }

        string normalized = requestPath.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.StartsWith("/wwwroot/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "/wwwroot" + normalized;
    }

    private static bool IsGetOrHeadMethod(string? method) =>
        string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateTryPaths(string requestPath)
    {
        if (requestPath.EndsWith('/'))
        {
            yield return requestPath + "index.html";
        }

        if (!Path.HasExtension(requestPath))
        {
            yield return requestPath + ".html";

            int lastIndexOfSlash = requestPath.LastIndexOf('/');
            if (lastIndexOfSlash != -1)
            {
                string prefixPart = requestPath[..lastIndexOfSlash];
                yield return prefixPart + ".html";
                yield return prefixPart + "/[id].html";
            }
        }
    }
}
