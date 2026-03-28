using System.Text;
using System.Text.Json;
using Android.Webkit;
using Microsoft.Maui.ApplicationModel;

namespace Chats.Mobile;

internal sealed class AppAssetWebViewClient : WebViewClient
{
    private const string LocalHost = "appassets.androidplatform.net";
    private readonly AssetFileResolver _assetFileResolver;
    private readonly ApiUrlSettingsStore _apiUrlSettingsStore;

    public AppAssetWebViewClient(AssetFileResolver assetFileResolver, ApiUrlSettingsStore apiUrlSettingsStore)
    {
        _assetFileResolver = assetFileResolver;
        _apiUrlSettingsStore = apiUrlSettingsStore;
    }

    public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        if (request?.Url == null)
        {
            return false;
        }

        if (string.Equals(request.Url.Host, LocalHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string targetUrl = request.Url.ToString() ?? string.Empty;
        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri? uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            MainThread.BeginInvokeOnMainThread(async () => await Launcher.Default.OpenAsync(uri));
            return true;
        }

        return false;
    }

    public override WebResourceResponse? ShouldInterceptRequest(Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        if (request?.Url == null ||
            !string.Equals(request.Url.Host, LocalHost, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return base.ShouldInterceptRequest(view, request);
        }

        string requestPath = Uri.UnescapeDataString(request.Url.EncodedPath ?? "/");
        if (string.Equals(requestPath, "/runtime-config.js", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRuntimeConfigResponse();
        }

        if (_assetFileResolver.TryResolveAssetPath(requestPath, request.Method, out string assetPath))
        {
            return CreateAssetResponse(assetPath);
        }

        return base.ShouldInterceptRequest(view, request);
    }

    private WebResourceResponse CreateRuntimeConfigResponse()
    {
        string apiUrl = _apiUrlSettingsStore.GetEffectiveApiUrl();
        string payload = $"window.__CHATS_RUNTIME_CONFIG__ = {{ apiUrl: {JsonSerializer.Serialize(apiUrl)} }};";
        MemoryStream stream = new(Encoding.UTF8.GetBytes(payload));
        WebResourceResponse response = new("application/javascript", "utf-8", stream);
        response.ResponseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = "no-store, no-cache, must-revalidate",
            ["Access-Control-Allow-Origin"] = "*",
        };
        return response;
    }

    private WebResourceResponse CreateAssetResponse(string assetPath)
    {
        string mimeType = GetMimeType(assetPath);
        string? encoding = IsTextMimeType(mimeType) ? "utf-8" : null;
        Stream stream = _assetFileResolver.OpenRead(assetPath);
        WebResourceResponse response = new(mimeType, encoding, stream);
        response.ResponseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = "public, max-age=31536000",
        };
        return response;
    }

    private static string GetMimeType(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        return extension switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".txt" => "text/plain",
            _ => MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension.TrimStart('.')) ?? "application/octet-stream",
        };
    }

    private static bool IsTextMimeType(string mimeType) =>
        mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase);
}
