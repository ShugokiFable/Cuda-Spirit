using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CudaSpirit.App.Infra;

namespace CudaSpirit.App.Overlay;

/// <summary>
/// The movable Advisor Overlay. It's the shared AI-advisor chat (synced with the AI Advisor tab)
/// in a draggable, click-through-able window. The header refresh asks "what should I do next?" so
/// that common question is one tap. Position persists across launches.
/// </summary>
public partial class TasksWindow : Window
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private IntPtr _hwnd;
    private bool _movable = true;

    public TasksWindow()
    {
        InitializeComponent();
        Opacity = Math.Clamp(_hub.Settings.Current.OverlayOpacity + 0.06, 0.5, 1.0);
        SourceInitialized += (_, _) => _hwnd = new WindowInteropHelper(this).Handle;
        Loaded += (_, _) => RestoreOrDockRight();
        Closed += (_, _) => SavePosition();
    }

    // ── Interactive vs click-through (driven by MainWindow / Ctrl+Alt+O) ──────

    public void SetInteractive(bool interactive)
    {
        _movable = interactive;
        if (_hwnd == IntPtr.Zero) _hwnd = new WindowInteropHelper(this).Handle;

        if (interactive)
        {
            NativeMethods.MakeInteractive(_hwnd);
            HeaderBar.Cursor = Cursors.SizeAll;
            SubtitleText.Text = "drag me · synced with the app";
        }
        else
        {
            NativeMethods.MakeClickThrough(_hwnd);
            HeaderBar.Cursor = Cursors.Arrow;
            SubtitleText.Text = "click-through | Ctrl+Alt+O to use";
        }
    }

    // ── Position ──────────────────────────────────────────────────────────────

    private void RestoreOrDockRight()
    {
        var wa = SystemParameters.WorkArea;
        Height = Math.Min(600, Math.Max(400, wa.Height - 120));

        var cfg = _hub.Settings.Current;
        if (cfg.TasksX >= 0 && cfg.TasksY >= 0 && IsOnScreen(cfg.TasksX, cfg.TasksY))
        {
            Left = cfg.TasksX;
            Top = cfg.TasksY;
        }
        else
        {
            Left = wa.Right - Width - 18;
            Top = wa.Top + 24;
        }
    }

    private static bool IsOnScreen(double x, double y)
    {
        var vs = SystemParameters.VirtualScreenWidth;
        var vt = SystemParameters.VirtualScreenHeight;
        var ox = SystemParameters.VirtualScreenLeft;
        var oy = SystemParameters.VirtualScreenTop;
        return x >= ox - 40 && y >= oy - 40 && x <= ox + vs - 60 && y <= oy + vt - 60;
    }

    public void SavePosition()
    {
        if (double.IsNaN(Left) || double.IsNaN(Top)) return;
        _hub.Settings.Update(cfg => { cfg.TasksX = Left; cfg.TasksY = Top; });
    }

    // ── Window buttons / drag ─────────────────────────────────────────────────

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_movable) return;
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* ignore rare re-entrancy */ }
            SavePosition();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnRefresh(object sender, RoutedEventArgs e) =>
        await _hub.Conversation.SendAsync(AdvisorConversation.PlanPrompt, showUser: true, displayAs: "What should I do next?");
}
