using System.IO;
using Microsoft.Web.WebView2.Core;

namespace CudaSpirit.App.Infra;

/// <summary>
/// Single shared WebView2 environment for the whole app. Every WebView2 in a process must use the
/// SAME user-data folder AND the SAME options, or CreateAsync throws. We have three consumers - the
/// AI advisor panel, the profile fetcher, and the in-app browser - so they all resolve their
/// environment here to stay consistent.
/// </summary>
public static class WebViewEnvironment
{
    private static Task<CoreWebView2Environment>? _shared;
    private static readonly object Gate = new();

    public static Task<CoreWebView2Environment> GetAsync()
    {
        lock (Gate)
        {
            return _shared ??= CreateAsync();
        }
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CudaSpirit", "WebView2");
        Directory.CreateDirectory(userData);

        // The anti-throttle flags let the hidden profile-fetcher window run the bot-check JS; they're
        // harmless for the visible advisor/browser windows.
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments =
                "--disable-features=CalculateNativeWinOcclusion,IntensiveWakeUpThrottling " +
                "--disable-background-timer-throttling --disable-renderer-backgrounding " +
                "--disable-backgrounding-occluded-windows"
        };
        return await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: userData, options);
    }
}
