using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chats.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ApiUrlSettingsStore>();
        builder.Services.AddSingleton<MainPage>();

#if ANDROID
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("ChatsWebView", (handler, view) =>
        {
            if (handler.MauiContext?.Services is not IServiceProvider services)
            {
                return;
            }

            if (handler.PlatformView is not Android.Webkit.WebView platformView)
            {
                return;
            }

            platformView.Settings.JavaScriptEnabled = true;
            platformView.Settings.DomStorageEnabled = true;
            platformView.Settings.DatabaseEnabled = true;
            platformView.Settings.MediaPlaybackRequiresUserGesture = false;
            platformView.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;

            Android.Webkit.CookieManager cookieManager = Android.Webkit.CookieManager.Instance!;
            cookieManager.SetAcceptCookie(true);
            cookieManager.SetAcceptThirdPartyCookies(platformView, true);

            AssetFileResolver assetFileResolver = new(platformView.Context!.Assets!);
            ApiUrlSettingsStore apiUrlSettingsStore = services.GetRequiredService<ApiUrlSettingsStore>();
            platformView.SetWebViewClient(new AppAssetWebViewClient(assetFileResolver, apiUrlSettingsStore));

            if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity activity)
            {
                platformView.SetWebChromeClient(new MauiWebChromeClient(activity));
            }
        });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
