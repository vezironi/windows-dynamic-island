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
    private readonly List<IntPtr> _hooks = new();
    private bool? _topAreaOccupied;
    private DispatcherTimer? _timer;

    public FullscreenMonitor() => _proc = (_, _, _, _, _, _, _) => Evaluate();

    public event Action<bool>? FullscreenChanged;
    public bool IsFullscreen { get; private set; }

    /// <summary>Raised (UI thread) when an app window starts / stops overlapping the island's spot.</summary>
    public event Action<bool>? TopAreaOccupiedChanged;

    /// <summary>Call on the UI thread: the out-of-context WinEvent hook needs its message loop.</summary>
    public void Start()
    {
        foreach (var (min, max) in new[]
                 {
                     (Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND),
                     (Native.EVENT_SYSTEM_MOVESIZEEND, Native.EVENT_SYSTEM_MOVESIZEEND),
                     (Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND),
                 })
            _hooks.Add(Native.SetWinEventHook(min, max, IntPtr.Zero, _proc, 0, 0, Native.WINEVENT_OUTOFCONTEXT));
        // Polling also catches maximize/restore, which raises no cheap event.
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Evaluate(),
            Dispatcher.CurrentDispatcher);
        _timer.Start();
        Evaluate();
    }

    private void Evaluate()
    {
        bool fullscreen, occupied;
        try { (fullscreen, occupied) = Detect(); }
        catch (Exception ex) { Log.Error("FullscreenMonitor", ex); return; }

        if (fullscreen != IsFullscreen)
        {
            IsFullscreen = fullscreen;
            FullscreenChanged?.Invoke(fullscreen);
        }
        if (occupied != _topAreaOccupied)
        {
            _topAreaOccupied = occupied;
            TopAreaOccupiedChanged?.Invoke(occupied);
        }
    }

    private static (bool Fullscreen, bool TopAreaOccupied) Detect()
    {
        Native.SHQueryUserNotificationState(out var q);
        var quns = (UserNotificationState)q;

        var primaryMon = Native.MonitorFromPoint(default, Native.MONITOR_DEFAULTTOPRIMARY);
        var primary = MonitorRect(primaryMon);
        var windows = TopLevelWindowsTopFirst();
        var occupied = TopAreaRules.IsOccupied(windows, IslandArea(primaryMon, primary));

        var hwnd = Native.GetForegroundWindow();
        var fg = hwnd == IntPtr.Zero ? null : Describe(hwnd);
        var fgMonitor = hwnd == IntPtr.Zero ? default : MonitorRect(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST));
        if (FullscreenRules.IsFullscreen(quns, fg, fgMonitor)) return (true, occupied);

        // Focus may be on another monitor while a game / F11 video fills the primary (where the island lives).
        return (FullscreenRules.IsMonitorCovered(windows, primary), occupied);
    }

    /// <summary>The compact island's spot (240×34 DIPs, top-center of the primary monitor) in physical pixels.</summary>
    private static PixelRect IslandArea(IntPtr monitor, PixelRect bounds)
    {
        var scale = Native.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1.0;
        var halfWidth = (int)Math.Ceiling(120 * scale);
        var center = (bounds.Left + bounds.Right) / 2;
        return new PixelRect(center - halfWidth, bounds.Top, center + halfWidth, bounds.Top + (int)Math.Ceiling(34 * scale));
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
        foreach (var h in _hooks) if (h != IntPtr.Zero) Native.UnhookWinEvent(h);
        _hooks.Clear();
    }
}
