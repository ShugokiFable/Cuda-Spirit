using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CudaSpirit.App.Infra;
using CudaSpirit.App.Overlay;
using CudaSpirit.App.Views;

namespace CudaSpirit.App;

public partial class MainWindow : Window
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private readonly Dictionary<string, UserControl> _views = new();
    private OverlayWindow? _overlay;
    private TasksWindow? _tasks;

    // Shared floating-window mode. false = interactive (drag/click), true = click-through (gameplay).
    private bool _clickThrough;
    private bool _selectingNav;

    private static readonly IReadOnlyDictionary<string, string> PageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["navigator"] = "Adventurer Navigator",
        ["recovery"] = "Returner & Reroll",
        ["rewards"] = "Rewards & Redemption",
        ["items"] = "Item Intel & Transfer",
        ["pearl"] = "Pearl Shop Guard",
        ["ui"] = "In-game UI Decoder",
        ["dashboard"] = "Dashboard",
        ["gear"] = "Gear & Progression",
        ["enhance"] = "Enhancement",
        ["calc"] = "Calculators",
        ["market"] = "Market & Alerts",
        ["data"] = "Live Data Center",
        ["routes"] = "Farm Route Optimizer",
        ["bosses"] = "Bosses & Events",
        ["trackers"] = "Trackers & Timers",
        ["advisor"] = "AI Advisor",
        ["web"] = "Web & References",
        ["customize"] = "Customize",
        ["settings"] = "Technical Settings"
    };

    public MainWindow()
    {
        _hub.Appearance.Apply();
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyShellPreferences();
            Navigate(_hub.Settings.Current.StartupPage);
            // Start background services (game window tracker, etc.).
            _hub.Start();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                CommandBox.Focus();
                CommandBox.SelectAll();
                e.Handled = true;
            }
        };
        // WindowChrome lets a maximized window bleed past the screen edge - pad it back in.
        StateChanged += (_, _) =>
        {
            RootBorder.Margin = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
            if (MaxBtn is not null)
                MaxBtn.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        };
    }

    // ── Custom title-bar controls ────────────────────────────────────────────
    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaxRestore(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (_selectingNav) return;
        if (sender is RadioButton { Tag: string tag })
            Navigate(tag);
    }

    private void Navigate(string tag)
    {
        if (Host is null) return;
        if (!_views.TryGetValue(tag, out var view))
        {
            view = tag switch
            {
                "navigator" => new NavigatorView(),
                "recovery" => new RecoveryCenterView(),
                "rewards" => new RewardsView(),
                "items" => new ItemIntelView(),
                "pearl" => new PearlShopView(),
                "ui" => new UiDecoderView(),
                "dashboard" => new DashboardView(),
                "gear" => new GearView(),
                "enhance" => new EnhancementView(),
                "calc" => new CalculatorsView(),
                "market" => new MarketView(),
                "data" => new DataCenterView(),
                "routes" => new RoutePlannerView(),
                "bosses" => new BossesView(),
                "trackers" => new TrackersView(),
                "advisor" => new AdvisorView(),
                "web" => new ReferencesView(),
                "customize" => new CustomizeView(),
                "settings" => new SettingsView(),
                _ => new DashboardView()
            };
            _views[tag] = view;
        }

        if (view is IRefreshable r) r.Refresh();
        Host.Content = view;
        PageCrumbText.Text = PageNames.TryGetValue(tag, out var pageName) ? pageName : "Cuda Spirit";
        SelectNav(tag);
        AnimatePageChange();
    }

    public void ApplyShellPreferences()
    {
        var s = _hub.Settings.Current;
        var compact = s.CompactNavigation;
        if (NavColumn is not null) NavColumn.Width = new GridLength(compact ? 68 : 256);
        Application.Current.Resources["NavButtonPad"] = compact ? new Thickness(10, 9, 10, 9) : new Thickness(14, 9, 14, 9);

        if (Host is not null)
        {
            Host.Margin = s.Density.Equals("compact", StringComparison.OrdinalIgnoreCase)
                ? new Thickness(20, 18, 24, 24)
                : s.Density.Equals("spacious", StringComparison.OrdinalIgnoreCase)
                    ? new Thickness(38, 30, 44, 44)
                    : new Thickness(28, 24, 34, 34);
        }

        if (NavBrandDetails is not null) NavBrandDetails.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (GuidedHeader is not null) GuidedHeader.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (ToolsHeader is not null) ToolsHeader.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (AppHeader is not null) AppHeader.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (SidebarSafetyText is not null) SidebarSafetyText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

        if (Nav is not null)
        {
            foreach (var button in Nav.Children.OfType<RadioButton>())
            {
                if (button.Content is not StackPanel panel) continue;
                panel.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                if (panel.Children.Count > 1 && panel.Children[1] is TextBlock label)
                    label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        TasksButton.Content = compact ? "AI" : "Advisor overlay";
        OverlayButton.Content = compact ? "HUD" : "HUD";
        PassThroughBtn.Content = compact
            ? (_clickThrough ? "PASS" : "LIVE")
            : (_clickThrough ? "Click-through" : "Interactive");

        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
    }

    private void AnimatePageChange()
    {
        if (_hub.Settings.Current.ReducedMotion || Host is null)
        {
            if (Host is not null)
            {
                Host.Opacity = 1;
                Host.RenderTransform = Transform.Identity;
            }
            return;
        }

        Host.Opacity = 0.84;
        var translate = new TranslateTransform(0, 3);
        Host.RenderTransform = translate;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Host.BeginAnimation(OpacityProperty, new DoubleAnimation(0.84, 1, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = ease
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(3, 0, TimeSpan.FromMilliseconds(135))
        {
            EasingFunction = ease
        });
    }

    private void SelectNav(string tag)
    {
        if (Nav is null) return;
        var match = Nav.Children.OfType<RadioButton>()
            .FirstOrDefault(x => string.Equals(x.Tag as string, tag, StringComparison.OrdinalIgnoreCase));
        if (match is null || match.IsChecked == true) return;
        _selectingNav = true;
        match.IsChecked = true;
        _selectingNav = false;
    }

    private void OnCommandTextChanged(object sender, TextChangedEventArgs e)
    {
        if (CommandPlaceholder is not null)
            CommandPlaceholder.Visibility = string.IsNullOrEmpty(CommandBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnCommandKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var query = CommandBox.Text.Trim();
        if (query.Length == 0) return;
        var q = query.ToLowerInvariant();
        var tag = q switch
        {
            _ when Has(q, "returning", "returner", "came back", "got back", "delete character", "reroll", "new class", "which class", "season character", "starting choice") => "recovery",
            _ when Has(q, "redeem", "coupon", "reward", "mail", "web storage", "attendance", "challenge") => "rewards",
            _ when Has(q, "item", "bind", "transfer", "storage", "warehouse", "sell", "keep", "open box", "maid", "magnus") => "items",
            _ when Has(q, "pearl", "cash shop", "value pack", "tent", "outfit", "buy") => "pearl",
            _ when Has(q, "theme", "appearance", "customize", "font", "density", "profile") => "customize",
            _ when Has(q, "where is", "menu", "hotkey", "ui", "screen", "worker", "pet", "fairy", "quest") => "ui",
            _ when Has(q, "farm", "route", "grind", "spot") => "routes",
            _ when Has(q, "boss", "world boss") => "bosses",
            _ when Has(q, "market", "price", "central market") => "market",
            _ when Has(q, "enhance", "failstack", "cron") => "enhance",
            _ when Has(q, "gear", "ap", "dp", "progression") => "gear",
            _ when Has(q, "data", "sync", "source", "database") => "data",
            _ when Has(q, "setting", "api", "openrouter") => "settings",
            _ => "advisor"
        };
        Navigate(tag);
        CommandBox.Clear();
        if (tag == "advisor")
            await _hub.Conversation.SendAsync(query, showUser: true);
    }

    private static bool Has(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    // ── Floating windows ──────────────────────────────────────────────────────

    private void OnToggleTasks(object sender, RoutedEventArgs e)
    {
        if (_tasks is { IsVisible: true })
        {
            _tasks.Close();
            _tasks = null;
            return;
        }
        _tasks = new TasksWindow { Owner = this };
        _tasks.Closed += (_, _) => _tasks = null;
        _tasks.Show();
        _tasks.SetInteractive(!_clickThrough);
    }

    private void OnToggleOverlay(object sender, RoutedEventArgs e)
    {
        if (_overlay is { IsVisible: true })
        {
            _overlay.ForceClose();
            _overlay = null;
            return;
        }
        _overlay = new OverlayWindow { Owner = this };
        _overlay.Closed += (_, _) => _overlay = null;
        // When F11 fires, ensure the Advisor overlay (TasksWindow) is visible so the user
        // can see the AI response streaming in the synced chat.
        _overlay.EnsureTasksVisible = () =>
        {
            if (_tasks is not { IsVisible: true })
                OnToggleTasks(this, new RoutedEventArgs());
        };
        _overlay.Show();
        // The overlay starts click-through; the F7 hotkey toggles interactive mode in-game.
        _overlay.SetInteractive(!_clickThrough);
    }

    /// <summary>Button handler for the single Interactive ⟷ Click-through toggle.</summary>
    private void OnTogglePassThrough(object sender, RoutedEventArgs e) => TogglePassThrough();

    /// <summary>
    /// Flip both floating windows between interactive (draggable / clickable) and click-through
    /// (mouse passes to the game, nothing movable). The overlay also responds to its own F7 hotkey.
    /// </summary>
    private void TogglePassThrough()
    {
        _clickThrough = !_clickThrough;
        _overlay?.SetInteractive(!_clickThrough);
        _tasks?.SetInteractive(!_clickThrough);
        var compact = _hub.Settings.Current.CompactNavigation;
        PassThroughBtn.Content = compact
            ? (_clickThrough ? "PASS" : "LIVE")
            : (_clickThrough ? "Click-through" : "Interactive");
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_overlay is { IsVisible: true }) { _overlay.ForceClose(); _overlay = null; }
        if (_tasks is { IsVisible: true }) { _tasks.Close(); _tasks = null; }
        base.OnClosed(e);
    }
}

/// <summary>Views implement this so the shell can refresh them when navigated to.</summary>
public interface IRefreshable
{
    void Refresh();
}