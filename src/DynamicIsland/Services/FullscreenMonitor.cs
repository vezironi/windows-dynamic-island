using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Interop;

namespace DynamicIsland.Services;

/// <summary>Watches the foreground window and reports when a fullscreen app is in front.</summary>
public sealed class FullscreenMonitor : IDisposable
{
    private readonly Native.WinEventProc _proc;   // kept in a field so the GC can't collect the callback
    private IntPtr _hook;
    private DispatcherTimer? _timer;

    public FullscreenMonitor() => _proc = (_, _, _, _, _, _, _) => Evaluate();

    public event Action<bool>? FullscreenChanged;
    public bool IsFullscreen { get; private set; }

    /// <summary>Call on the UI thread: the out-of-context WinEvent hook needs its message loop.</summary>
    public void Start()
    {
        _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _proc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Evaluate(),
            Dispatcher.CurrentDispatcher);
        _timer.Start();
        Evaluate();
    }

    private void Evaluate()
    {
        bool now;
        try { now = Detect(); }
        catch (Exception ex) { Log.Error("FullscreenMonitor", ex); return; }
        if (now == IsFullscreen) return;
        IsFullscreen = now;
        FullscreenChanged?.Invoke(now);
    }

    private static bool Detect()
    {
        Native.SHQueryUserNotificationState(out var q);
        var quns = (UserNotificationState)q;

        var hwnd = Native.GetForegroundWindow();
        var fg = hwnd == IntPtr.Zero ? null : Describe(hwnd);
        var fgMonitor = hwnd == IntPtr.Zero ? default : MonitorRect(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST));
        if (FullscreenRules.IsFullscreen(quns, fg, fgMonitor)) return true;

        // Focus may be on another monitor while a game / F11 video fills the primary (where the island lives).
        var primary = MonitorRect(Native.MonitorFromPoint(default, Native.MONITOR_DEFAULTTOPRIMARY));
        return FullscreenRules.IsMonitorCovered(TopLevelWindowsTopFirst(), primary);
    }

    private static ForegroundWindow Describe(IntPtr hwnd)
    {
        var cls = new StringBuilder(256);
        Native.GetClassName(hwnd, cls, cls.Capacity);
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out Native.RECT r,
                Marshal.SizeOf<Native.RECT>()) != 0)
            Native.GetWindowRect(hwnd, out r);
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        return new ForegroundWindow(cls.ToString(), new PixelRect(r.Left, r.Top, r.Right, r.Bottom),
            Native.IsZoomed(hwnd), pid == (uint)Environment.ProcessId);
    }

    private static PixelRect MonitorRect(IntPtr mon)
    {
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfo(mon, ref mi);
        return new PixelRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
    }

    /// <summary>Visible, real top-level windows in z-order (top first). Skips minimized, cloaked
    /// (other virtual desktops / suspended UWP), tool and click-through overlay windows.</summary>
    private static List<ForegroundWindow> TopLevelWindowsTopFirst()
    {
        var list = new List<ForegroundWindow>();
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
            var ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
            if ((ex & (Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT)) != 0) return true;
            if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var w = Describe(h);
            if (w.Bounds.Right <= w.Bounds.Left || w.Bounds.Bottom <= w.Bounds.Top) return true;
            list.Add(w);
            return list.Count < 64;   // the answer is always among the top few
        }, IntPtr.Zero);
        return list;
    }

    public void Dispose()
    {
        _timer?.Stop();
        if (_hook != IntPtr.Zero) Native.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }
}
