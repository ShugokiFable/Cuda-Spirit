using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CudaSpirit.App.Infra;

/// <summary>
/// Loads a page in a hidden WebView2 (real Chromium) and returns the rendered HTML. This is how we
/// read the official Black Desert adventurer profile now that Pearl Abyss put the site behind an
/// Imperva Incapsula bot-check: a raw HttpClient just gets a 403 challenge page, but a real browser
/// engine solves the challenge automatically, so the actual profile HTML is available to scrape.
///
/// Still ToS-safe: this loads the same public web page a browser would; it does not touch the game
/// client, its memory, or its traffic. Must be called on the UI thread (WebView2 is a UI control).
/// </summary>
public static class WebViewFetcher
{
    /// <summary>
    /// Navigate to <paramref name="url"/> and poll the DOM until <paramref name="ready"/> is satisfied
    /// (e.g. the profile markup has appeared), then return the full outerHTML. Returns whatever loaded
    /// if it times out.
    /// </summary>
    public static async Task<string> FetchHtmlAsync(string url, Func<string, bool> ready, int timeoutMs = 25000)
    {
        // Off-screen, fully transparent host window - the Chromium engine still runs and executes JS.
        var win = new Window
        {
            Width = 520,
            Height = 360,
            Left = -4000,
            Top = -4000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            IsHitTestVisible = false
        };
        var wv = new WebView2();
        win.Content = wv;
        win.Show();

        try
        {
            var env = await WebViewEnvironment.GetAsync();
            await wv.EnsureCoreWebView2Async(env);
            wv.CoreWebView2.Settings.AreDevToolsEnabled = false;
            wv.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            wv.CoreWebView2.Navigate(url);

            var start = Environment.TickCount64;
            string html = "";
            int rechallenges = 0;
            while (Environment.TickCount64 - start < timeoutMs)
            {
                await Task.Delay(2200);
                try
                {
                    var raw = await wv.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
                    if (!string.IsNullOrEmpty(raw) && raw != "null")
                    {
                        // ExecuteScriptAsync returns the result JSON-encoded (a quoted, escaped string).
                        html = JsonSerializer.Deserialize<string>(raw) ?? "";
                        if (ready(html)) return html;
                    }
                    // Still the bot-check page: it has set its clearance cookie by now, so re-request
                    // the URL - the follow-up request carries the cookie and returns the real page.
                    if (++rechallenges <= 6) wv.CoreWebView2.Navigate(url);
                }
                catch
                {
                    // Page is mid-navigation (challenge redirecting) - keep polling.
                }
            }
            return html;
        }
        finally
        {
            win.Close();
        }
    }
}
