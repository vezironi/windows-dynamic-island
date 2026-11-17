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
        if (hwnd == IntPtr.Zero) return FullscreenRules.IsFullscreen(quns, null, default);

        var cls = new StringBuilder(256);
        Native.GetClassName(hwnd, cls, cls.Capacity);

        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var r,
                Marshal.SizeOf<Native.RECT>()) != 0)
            Native.GetWindowRect(hwnd, out r);

        Native.GetWindowThreadProcessId(hwnd, out var pid);

        var mon = Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST);
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfo(mon, ref mi);

        var fg = new ForegroundWindow(cls.ToString(),
            new PixelRect(r.Left, r.Top, r.Right, r.Bottom),
            Native.IsZoomed(hwnd),
            pid == (uint)Environment.ProcessId);
        var monitor = new PixelRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
        return FullscreenRules.IsFullscreen(quns, fg, monitor);
    }

    public void Dispose()
    {
        _timer?.Stop();
        if (_hook != IntPtr.Zero) Native.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }
}
