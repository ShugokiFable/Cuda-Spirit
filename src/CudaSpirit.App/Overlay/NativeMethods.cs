using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace CudaSpirit.App.Overlay;

/// <summary>
/// P/Invoke used purely for window presentation: making the overlay click-through, registering
/// global hotkeys, and finding the game's window rectangle. Note what this does NOT do - it never
/// opens the game process, reads its memory, or touches its handles. It only reads window geometry
/// and manages overlay window styles, the same as any window manager.
/// </summary>
internal static class NativeMethods
{
    // ── Window style constants ───────────────────────────────────────────────
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020; // click-through
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;  // hide from alt-tab

    // ── Hotkey constants ────────────────────────────────────────────────────
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_NOREPEAT = 0x4000;
    public const uint VK_F6 = 0x75;
    public const uint VK_F7 = 0x76;
    public const uint VK_F8 = 0x77;
    public const uint VK_F9 = 0x78;
    public const uint VK_F10 = 0x79;
    public const uint VK_F11 = 0x7A;
    public const uint VK_F12 = 0x7B;
    public const uint VK_O = 0x4F;

    // ── Layered window alpha ────────────────────────────────────────────────
    public const uint LWA_ALPHA = 0x2;

    // ── SetWindowPos flags ──────────────────────────────────────────────────
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
                       SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

    // ── Structs ─────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    // ── P/Invoke declarations ───────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Known BDO executable names (various regions / launchers).
    private static readonly string[] BdoProcessNames =
    {
        "BlackDesert", "BlackDesert64", "BlackDesert32",
        "BlackDesertOnline", "BlackDesertOnline64",
        "BDO", "BDO64", "BDO32",
        "PearlAbyss", "GameDesert"
    };

    // ── Window style helpers ────────────────────────────────────────────────

    /// <summary>Re-read the frame after an EXSTYLE change; without this some style bits never take effect.</summary>
    private static void FlushStyle(IntPtr hwnd) =>
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

    /// <summary>Make a window ignore mouse input (click-through) and stay out of alt-tab.</summary>
    public static void MakeClickThrough(IntPtr hwnd)
    {
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        FlushStyle(hwnd);
    }

    /// <summary>
    /// Undo click-through so the overlay can receive clicks. Remove transparent AND no-activate
    /// so the HUD can receive clicks, wheel scrolling and focus. Keep layered + toolwindow.
    /// Must flush the frame or the transparent bit lingers.
    /// </summary>
    public static void MakeInteractive(IntPtr hwnd)
    {
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex &= ~(long)(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        FlushStyle(hwnd);
    }

    /// <summary>Uniform window alpha (works on normal windows, no WPF AllowsTransparency needed). 255 = opaque.</summary>
    public static void SetWindowAlpha(IntPtr hwnd, byte alpha)
    {
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if (alpha >= 255)
        {
            if ((ex & WS_EX_LAYERED) != 0)
            {
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex & ~(long)WS_EX_LAYERED));
                FlushStyle(hwnd);
            }
            return;
        }
        if ((ex & WS_EX_LAYERED) == 0)
        {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_LAYERED));
            FlushStyle(hwnd);
        }
        SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
    }

    // ── Client bounds (screen-space rectangle of a window's client area) ───

    /// <summary>Screen-space rectangle of a window's client area. Returns null if the window is gone.</summary>
    public static Rectangle? GetClientBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return null;
        if (!GetClientRect(hwnd, out var rc)) return null;
        var origin = new POINT { X = rc.Left, Y = rc.Top };
        if (!ClientToScreen(hwnd, ref origin)) return null;
        int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
        if (w <= 0 || h <= 0) return null;
        return new Rectangle(origin.X, origin.Y, w, h);
    }

    // ── Window finders ──────────────────────────────────────────────────────

    /// <summary>Find the first visible top-level window whose title contains <paramref name="titlePart"/>.</summary>
    public static IntPtr FindWindowByTitlePart(string titlePart)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            if (sb.ToString().Contains(titlePart, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// Multi-strategy BDO window finder. Tries:
    ///  1. The user-configured title substring
    ///  2. Common BDO title variants (with/without space, Online suffix, region tags)
    ///  3. Process-name based search (matches the executable name to the owning process)
    /// </summary>
    public static IntPtr FindBdoWindow(string userTitlePart)
    {
        IntPtr h = FindWindowByTitlePart(userTitlePart);
        if (h != IntPtr.Zero) return h;

        string[] fallbacks = { "BlackDesert", "Black Desert", "Black Desert Online", "검은사막" };
        foreach (var fb in fallbacks)
        {
            if (fb.Equals(userTitlePart, StringComparison.OrdinalIgnoreCase)) continue;
            h = FindWindowByTitlePart(fb);
            if (h != IntPtr.Zero) return h;
        }

        return FindWindowByProcessName();
    }

    private static IntPtr FindWindowByProcessName()
    {
        var known = new HashSet<string>(BdoProcessNames, StringComparer.OrdinalIgnoreCase);
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return true;
            try
            {
                var proc = Process.GetProcessById((int)pid);
                if (known.Contains(proc.ProcessName))
                {
                    int len = GetWindowTextLength(hWnd);
                    if (len > 0) { found = hWnd; return false; }
                }
            }
            catch { /* process may have exited */ }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>Get all visible top-level window titles (diagnostic helper).</summary>
    public static List<string> GetVisibleWindowTitles()
    {
        var titles = new List<string>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (!string.IsNullOrWhiteSpace(title))
                titles.Add(title);
            return true;
        }, IntPtr.Zero);
        return titles;
    }
}