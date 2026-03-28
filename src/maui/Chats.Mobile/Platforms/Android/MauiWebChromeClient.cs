using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Webkit;

namespace Chats.Mobile;

internal sealed class MauiWebChromeClient : WebChromeClient
{
    private const int FileChooserRequestCode = 7412;
    private static IValueCallback? _pendingFilePathCallback;
    private readonly Activity _activity;

    public MauiWebChromeClient(Activity activity)
    {
        _activity = activity;
    }

    public override bool OnShowFileChooser(Android.Webkit.WebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
    {
        _pendingFilePathCallback?.OnReceiveValue(null);
        _pendingFilePathCallback = filePathCallback;

        try
        {
            Intent chooserIntent = fileChooserParams?.CreateIntent() ?? BuildFallbackChooserIntent();
            _activity.StartActivityForResult(chooserIntent, FileChooserRequestCode);
            return true;
        }
        catch (ActivityNotFoundException)
        {
            _pendingFilePathCallback = null;
            return false;
        }
    }

    public static bool HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != FileChooserRequestCode || _pendingFilePathCallback == null)
        {
            return false;
        }

        _pendingFilePathCallback.OnReceiveValue(FileChooserParams.ParseResult((int)resultCode, data));
        _pendingFilePathCallback = null;
        return true;
    }

    private static Intent BuildFallbackChooserIntent()
    {
        Intent intent = new(Intent.ActionGetContent);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        return Intent.CreateChooser(intent, "Select file") ?? intent;
    }
}
