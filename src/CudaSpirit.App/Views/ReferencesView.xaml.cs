using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace CudaSpirit.App.Views;

/// <summary>
/// In-app browser for the big BDO reference sites (Garmoth, QuestLog, BDO Codex, Bdolytics, node
/// maps). Rather than re-hosting their huge live databases, we embed the real sites via WebView2 so
/// they're always current - with the AI advisor a click away. It's just a browser pointed at public
/// pages; no game process or memory is touched.
/// </summary>
public partial class ReferencesView : UserControl, IRefreshable
{
    private const string Home = "https://garmoth.com";
    private bool _ready;

    public ReferencesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void Refresh() { /* browser keeps its own state */ }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;
        try
        {
            var env = await Infra.WebViewEnvironment.GetAsync();
            await Web.EnsureCoreWebView2Async(env);

            // Keep pop-ups (target=_blank) inside this browser instead of spawning OS windows.
            Web.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Web.CoreWebView2.Navigate(args.Uri);
            };
            Web.CoreWebView2.SourceChanged += (_, _) => UrlBox.Text = Web.Source?.ToString() ?? "";
            Web.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                BackBtn.IsEnabled = Web.CoreWebView2.CanGoBack;
                FwdBtn.IsEnabled = Web.CoreWebView2.CanGoForward;
            };

            _ready = true;
            Web.CoreWebView2.Navigate(Home);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The WebView2 runtime couldn't start. Install the Microsoft Edge WebView2 Runtime, then reopen this tab.\n\n" + ex.Message,
                "Web / References", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Navigate(string url)
    {
        if (!_ready || Web.CoreWebView2 is null) return;
        if (!url.Contains("://", StringComparison.Ordinal)) url = "https://" + url;
        try { Web.CoreWebView2.Navigate(url); } catch { /* invalid URL */ }
    }

    private void OnGoSite(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url }) Navigate(url);
    }

    private void OnGo(object sender, RoutedEventArgs e) => Navigate(UrlBox.Text.Trim());

    private void OnUrlKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; Navigate(UrlBox.Text.Trim()); }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_ready && Web.CoreWebView2 is { CanGoBack: true }) Web.CoreWebView2.GoBack();
    }

    private void OnForward(object sender, RoutedEventArgs e)
    {
        if (_ready && Web.CoreWebView2 is { CanGoForward: true }) Web.CoreWebView2.GoForward();
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        if (_ready) Web.CoreWebView2?.Reload();
    }
}
