using System.Diagnostics;
using System.Drawing;
using CudaSpirit.App.Overlay;

namespace CudaSpirit.App.Infra;

/// <summary>
/// Tracks the BDO game window (position, size, focus) by process name and window title - no handles
/// into the game beyond public user32 window queries. The overlay and screen capture consume
/// <see cref="ClientBounds"/>.
/// </summary>
public sealed class GameWindowTracker : IDisposable
{
    private CancellationTokenSource? _cts;

    public IntPtr GameHwnd { get; private set; }
    public Rectangle? ClientBounds { get; private set; }
    public bool GameRunning => GameHwnd != IntPtr.Zero;
    public bool GameFocused => GameRunning && NativeMethods.GetForegroundWindow() == GameHwnd;

    /// <summary>Raises whenever the tracked window moves, resizes, or appears/disappears.</summary>
    public event Action? Changed;

    private readonly string _gameWindowTitle;

    public GameWindowTracker(string gameWindowTitle = "Black Desert")
    {
        _gameWindowTitle = gameWindowTitle;
    }

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (true)
        {
            try { if (!await timer.WaitForNextTickAsync(ct)) break; }
            catch (OperationCanceledException) { break; }
            try { Poll(); } catch { /* transient process/window races */ }
        }
    }

    private void Poll()
    {
        var hwnd = NativeMethods.FindBdoWindow(_gameWindowTitle);
        var bounds = hwnd == IntPtr.Zero ? null : NativeMethods.GetClientBounds(hwnd);
        if (hwnd != GameHwnd || bounds != ClientBounds)
        {
            GameHwnd = hwnd;
            ClientBounds = bounds;
            Changed?.Invoke();
        }
    }

    /// <summary>Capture target: game client area when running, else the primary screen.</summary>
    public Rectangle CaptureBounds =>
        ClientBounds
        ?? System.Windows.Forms.Screen.PrimaryScreen?.Bounds
        ?? new Rectangle(0, 0, 1920, 1080);

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}