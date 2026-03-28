using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Chats.Mobile;

public partial class MainPage : ContentPage
{
    private readonly ApiUrlSettingsStore _apiUrlSettingsStore;
    private const string LocalHost = "appassets.androidplatform.net";

    public MainPage(ApiUrlSettingsStore apiUrlSettingsStore)
    {
        InitializeComponent();
        _apiUrlSettingsStore = apiUrlSettingsStore;
        AppWebView.Source = new UrlWebViewSource
        {
            Url = $"https://{LocalHost}/index.html",
        };
    }

    private async void OnApiSettingsClicked(object? sender, EventArgs e)
    {
        string currentUrl = _apiUrlSettingsStore.GetEffectiveApiUrl();
        string action = await DisplayActionSheetAsync(
            "API URL",
            "Cancel",
            null,
            "Edit API URL",
            "Reset to build default");

        switch (action)
        {
            case "Edit API URL":
            {
                string? input = await DisplayPromptAsync(
                    "API URL",
                    "Enter the backend base URL.",
                    "Save",
                    "Cancel",
                    placeholder: currentUrl,
                    initialValue: currentUrl,
                    keyboard: Keyboard.Url);

                if (input == null)
                {
                    return;
                }

                if (!_apiUrlSettingsStore.TryNormalize(input, out string normalized))
                {
                    await DisplayAlertAsync("Invalid URL", "Enter an absolute http or https URL.", "OK");
                    return;
                }

                _apiUrlSettingsStore.SetOverride(normalized);
                AppWebView.Reload();
                break;
            }
            case "Reset to build default":
                _apiUrlSettingsStore.ResetOverride();
                AppWebView.Reload();
                break;
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out Uri? uri))
        {
            return;
        }

        if (string.Equals(uri.Host, LocalHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await Launcher.Default.OpenAsync(uri);
        }
    }
}
