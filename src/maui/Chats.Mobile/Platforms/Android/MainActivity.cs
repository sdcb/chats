using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Chats.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (MauiWebChromeClient.HandleActivityResult(requestCode, resultCode, data))
        {
            return;
        }

        base.OnActivityResult(requestCode, resultCode, data);
    }
}
