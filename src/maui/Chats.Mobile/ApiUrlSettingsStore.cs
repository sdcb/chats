using System.Reflection;
using Microsoft.Maui.Storage;

namespace Chats.Mobile;

public sealed class ApiUrlSettingsStore
{
    private const string OverrideKey = "api_url_override";
    private readonly string _defaultApiUrl;

    public ApiUrlSettingsStore()
    {
        _defaultApiUrl = typeof(ApiUrlSettingsStore).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "DefaultApiUrl")?
            .Value?
            .Trim() ?? string.Empty;
    }

    public string DefaultApiUrl => _defaultApiUrl;

    public string? GetOverride()
    {
        string? value = Preferences.Default.Get<string?>(OverrideKey, null);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string GetEffectiveApiUrl() => GetOverride() ?? _defaultApiUrl;

    public void SetOverride(string apiUrl)
    {
        Preferences.Default.Set(OverrideKey, apiUrl.Trim());
    }

    public void ResetOverride()
    {
        Preferences.Default.Remove(OverrideKey);
    }

    public bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/');
        return true;
    }
}
