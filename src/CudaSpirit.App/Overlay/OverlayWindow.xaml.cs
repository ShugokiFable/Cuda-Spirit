using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CudaSpirit.App.Infra;
using Point = System.Windows.Point;

namespace CudaSpirit.App.Overlay;

/// <summary>
/// Transparent, always-on-top, fully click-through overlay that covers the game window (or primary
/// screen when the game isn't running). F11 captures the screen and sends the screenshot into the
/// shared AI Advisor conversation (synced between the Advisor tab and the TasksWindow overlay).
/// F7 toggles interactive mode so the user can drag panels; F6 locks/unlocks panel positions;
/// F9 hides/shows the overlay. Requires Borderless/Windowed game mode
/// (exclusive fullscreen hides overlays by design).
/// </summary>
public partial class OverlayWindow : Window
{
    // ── Hotkey IDs (must be unique per overlay instance) ─────────────────────
    private const int HotkeyHide = 0xC0DA;
    private const int HotkeyAnalyze = 0xC0DB;
    private const int HotkeyInteractive = 0xC0DC;
    private const int HotkeyLock = 0xC0DD;

    private readonly ServiceHub _hub = ServiceHub.Instance;
    private readonly DispatcherTimer _followTimer;
    private readonly DispatcherTimer _hudTimer;
    private readonly DispatcherTimer _toastTimer;

    private IntPtr _hwnd;
    private bool _panelsVisible = true;
    private bool _interactiveMode;
    private bool _panelsLocked = true;

    // Drag state
    private UIElement? _dragTarget;
    private TranslateTransform? _dragTransform;
    private Point _dragStart;
    private Point _dragOrigin;

    /// <summary>
    /// Callback set by MainWindow. When F11 fires and the TasksWindow (advisor overlay) isn't
    /// visible, the overlay calls this to ask MainWindow to open it - so the user can see the
    /// AI response streaming in the synced chat.
    /// </summary>
    public Action? EnsureTasksVisible { get; set; }

    public OverlayWindow()
    {
        InitializeComponent();

        // Follow the game window every 500ms with a low-noise polling loop.
        _followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _followTimer.Tick += (_, _) => FollowGameWindow();
        _followTimer.Start();

        // HUD stats tick every 1s.
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _hudTimer.Tick += (_, _) => TickHud();
        _hudTimer.Start();

        // Toast auto-hide after 4 seconds.
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast.Visibility = Visibility.Collapsed; };

        SourceInitialized += OnSourceInitialized;
    }

    // ── Win32 initialization ────────────────────────────────────────────────

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeClickThrough(_hwnd);

        // Register global hotkeys that work even while the game has focus.
        NativeMethods.RegisterHotKey(_hwnd, HotkeyHide, NativeMethods.MOD_NONE, NativeMethods.VK_F9);
        NativeMethods.RegisterHotKey(_hwnd, HotkeyAnalyze, NativeMethods.MOD_NONE, NativeMethods.VK_F11);
        NativeMethods.RegisterHotKey(_hwnd, HotkeyInteractive, NativeMethods.MOD_NONE, NativeMethods.VK_F7);
        NativeMethods.RegisterHotKey(_hwnd, HotkeyLock, NativeMethods.MOD_NONE, NativeMethods.VK_F6);

        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        // Wire drag handlers for all draggable panels (only active when unlocked in interactive mode).
        WireDrag(HudPanel, HudTransform);
        WireDrag(StatusChip, ChipTransform);
        WireDrag(InteractiveBanner, BannerTransform);

        FollowGameWindow();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HotkeyHide:
                    _panelsVisible = !_panelsVisible;
                    Opacity = _panelsVisible ? 1.0 : 0.0;
                    handled = true;
                    break;
                case HotkeyAnalyze:
                    _ = TriggerAnalyzeScreenAsync();
                    handled = true;
                    break;
                case HotkeyInteractive:
                    ToggleInteractiveMode();
                    handled = true;
                    break;
                case HotkeyLock:
                    _panelsLocked = !_panelsLocked;
                    UpdateChipText();
                    ShowToast(_panelsLocked ? "Panels locked" : "Panels unlocked - drag to reposition");
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    // ── Game-window following ───────────────────────────────────────────────

    /// <summary>Resize and position the overlay to match the game client area (or primary screen).</summary>
    private void FollowGameWindow()
    {
        var bounds = _hub.Tracker.ClientBounds;
        Rectangle rc;
        if (bounds is not { } b)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new Rectangle(0, 0, 1280, 720);
            rc = screen;
        }
        else
        {
            rc = b;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        Left = rc.X / dpi.DpiScaleX;
        Top = rc.Y / dpi.DpiScaleY;
        Width = rc.Width / dpi.DpiScaleX;
        Height = rc.Height / dpi.DpiScaleY;
    }

    // ── HUD stats tick (1 Hz) ───────────────────────────────────────────────

    private void TickHud()
    {
        var g = _hub.Grind;
        if (g.IsRunning)
        {
            SilverHrText.Text = Short(g.SilverPerHour);
            TrashText.Text = g.TrashCount.ToString("N0");
            ZoneText.Text = $"{g.Zone} · {g.Elapsed:hh\\:mm\\:ss}";
        }
        else
        {
            SilverHrText.Text = "-";
            TrashText.Text = "0";
            var gameHwnd = _hub.Tracker.GameHwnd;
            if (gameHwnd != IntPtr.Zero)
            {
                var zone = _hub.Live.Current.GrindZone;
                ZoneText.Text = string.IsNullOrWhiteSpace(zone) ? "In-game" : $"In-game: {zone}";
            }
            else
            {
                ZoneText.Text = "No active session";
            }
        }

        var boss = _hub.Bosses.GetUpcoming(1).FirstOrDefault();
        BossText.Text = boss is null ? "-"
            : boss.IsLive ? $"{boss.Name} - LIVE"
            : $"{boss.Name} in {FormatShort(boss.TimeUntil)}";

        var step = _hub.Progression.Suggest(_hub.Live.Current).FirstOrDefault();
        NextStepText.Text = step?.Title ?? "-";
    }

    // ── Toast notifications ─────────────────────────────────────────────────

    private void ShowToast(string text)
    {
        ToastText.Text = text;
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // ── Interactive mode toggle (F7) ────────────────────────────────────────

    private void ToggleInteractiveMode()
    {
        _interactiveMode = !_interactiveMode;
        if (_interactiveMode)
        {
            NativeMethods.MakeInteractive(_hwnd);
            InteractiveBanner.Visibility = Visibility.Visible;
            _panelsLocked = true;
            UpdateChipText();
        }
        else
        {
            NativeMethods.MakeClickThrough(_hwnd);
            InteractiveBanner.Visibility = Visibility.Collapsed;
        }
    }

    // ── Panel drag (F6 lock/unlock) ─────────────────────────────────────────

    private void WireDrag(Border border, TranslateTransform transform)
    {
        border.MouseLeftButtonDown += (_, e) =>
        {
            if (_panelsLocked || !_interactiveMode) return;
            StartDrag(border, transform, e);
        };
        border.MouseMove += (_, e) =>
        {
            if (_dragTarget != border) return;
            ContinueDrag(e);
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            if (_dragTarget != border) return;
            EndDrag();
        };
    }

    private void StartDrag(UIElement element, TranslateTransform transform, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        _dragTarget = element;
        _dragTransform = transform;
        _dragStart = pos;
        _dragOrigin = new Point(transform.X, transform.Y);
        element.CaptureMouse();
        e.Handled = true;
    }

    private void ContinueDrag(MouseEventArgs e)
    {
        if (_dragTransform is null) return;
        var pos = e.GetPosition(this);
        _dragTransform.X = _dragOrigin.X + (pos.X - _dragStart.X);
        _dragTransform.Y = _dragOrigin.Y + (pos.Y - _dragStart.Y);
        e.Handled = true;
    }

    private void EndDrag()
    {
        _dragTarget?.ReleaseMouseCapture();
        _dragTarget = null;
        _dragTransform = null;
    }

    private void UpdateChipText()
    {
        ChipText.Text = _panelsLocked
            ? "F6 lock · F7 HUD · F9 hide · F11 analyze"
            : "F6 LOCK · F7 HUD · F9 hide · F11 analyze";
    }

    // ── AI screen analysis (F11) → feeds into the synced Advisor chat ───────

    /// <summary>
    /// Capture the current game window (or primary screen) and send the screenshot into the
    /// shared AdvisorConversation. The AI response streams into both the Advisor tab and the
    /// TasksWindow overlay - they share the same conversation. If the TasksWindow isn't open,
    /// we ask MainWindow to open it so the user sees the response.
    /// </summary>
    private async Task TriggerAnalyzeScreenAsync()
    {
        var s = _hub;

        if (string.IsNullOrWhiteSpace(s.Settings.Current.OpenRouterApiKey))
        {
            ShowToast("[!] No API key - add one in Settings to use screen analysis.");
            return;
        }

        if (s.Conversation.IsBusy)
        {
            ShowToast("[...] Advisor is busy - wait for the current response to finish.");
            return;
        }

        var bounds = s.Tracker.CaptureBounds;
        var dataUrl = ScreenCapture.CaptureRegionAsDataUrl(bounds);
        if (dataUrl is null)
        {
            ShowToast("[!] Capture failed - is the game in Borderless/Windowed?");
            return;
        }

        ShowToast($"[AI] F11 screen captured ({bounds.Width}x{bounds.Height}) - sending to AI Advisor...");

        // Ensure the TasksWindow (advisor overlay) is visible so the user can see the streaming response.
        EnsureTasksVisible?.Invoke();

        // Send the screenshot into the shared conversation - it appears in both the Advisor tab
        // and the TasksWindow overlay through the shared conversation service.
        await s.Conversation.SendWithImageAsync(
            "F11 screen analysis: " +
            "Read everything visible on this Black Desert screen - gear, stats, inventory, map, " +
            "chat, enhancement window, whatever is on screen - and give concise, actionable advice. " +
            "Be specific about what you see.",
            dataUrl);

        ShowToast("[OK] Screen analysis complete - check the AI Advisor.");
    }

    // ── Public API (called from MainWindow) ─────────────────────────────────

    /// <summary>Set interactive (click-receiving) or click-through mode. Driven by MainWindow.</summary>
    public void SetInteractive(bool interactive)
    {
        if (_hwnd == IntPtr.Zero) _hwnd = new WindowInteropHelper(this).Handle;
        _interactiveMode = interactive;

        if (interactive)
        {
            NativeMethods.MakeInteractive(_hwnd);
            InteractiveBanner.Visibility = Visibility.Visible;
            _panelsLocked = true;
            UpdateChipText();
        }
        else
        {
            NativeMethods.MakeClickThrough(_hwnd);
            InteractiveBanner.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Unconditionally close the overlay, stopping all timers.</summary>
    public void ForceClose()
    {
        _followTimer.Stop();
        _hudTimer.Stop();
        _toastTimer.Stop();
        Close();
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _followTimer.Stop();
        _hudTimer.Stop();
        _toastTimer.Stop();

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyHide);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyAnalyze);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyInteractive);
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyLock);
        }
        base.OnClosed(e);
    }

    // ── Formatting helpers ──────────────────────────────────────────────────

    private static string Short(long v)
    {
        if (v >= 1_000_000_000) return $"{v / 1_000_000_000.0:0.0}B";
        if (v >= 1_000_000) return $"{v / 1_000_000.0:0.0}M";
        if (v >= 1_000) return $"{v / 1_000.0:0.0}K";
        return v.ToString();
    }

    private static string FormatShort(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h{t.Minutes:00}m" : $"{t.Minutes}m";
    }
}