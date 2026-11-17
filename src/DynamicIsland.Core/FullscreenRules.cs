namespace DynamicIsland.Core;

/// <summary>Values of the Win32 QUERY_USER_NOTIFICATION_STATE enum.</summary>
public enum UserNotificationState
{
    NotPresent = 1,
    Busy = 2,
    RunningD3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    App = 7,
}

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public bool Intersects(PixelRect o) =>
        Left < o.Right && o.Left < Right && Top < o.Bottom && o.Top < Bottom;

    public bool Contains(PixelRect o) =>
        Left <= o.Left && Top <= o.Top && Right >= o.Right && Bottom >= o.Bottom;
}

/// <param name="Bounds">DWM extended-frame bounds of the window, in physical pixels.</param>
public sealed record ForegroundWindow(string ClassName, PixelRect Bounds, bool IsMaximized, bool IsOwnProcess);

public static class FullscreenRules
{
    internal static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    /// <summary>
    /// True when the top-most real window on <paramref name="monitor"/> (z-order, top first) is a
    /// fullscreen one — catches a game on the primary monitor while focus is on another monitor.
    /// </summary>
    public static bool IsMonitorCovered(IEnumerable<ForegroundWindow> zOrder, PixelRect monitor)
    {
        foreach (var w in zOrder)
        {
            if (w.IsOwnProcess || ShellClasses.Contains(w.ClassName) || !w.Bounds.Intersects(monitor)) continue;
            return !w.IsMaximized && w.Bounds.Contains(monitor);
        }
        return false;
    }

    public static bool IsFullscreen(UserNotificationState quns, ForegroundWindow? fg, PixelRect monitorOfFg)
    {
        if (quns is UserNotificationState.Busy or UserNotificationState.RunningD3DFullScreen
            or UserNotificationState.PresentationMode)
            return true;

        if (fg is null || fg.IsOwnProcess || fg.IsMaximized || ShellClasses.Contains(fg.ClassName))
            return false;

        return fg.Bounds.Contains(monitorOfFg);
    }
}

public static class TopAreaRules
{
    /// <summary>True when any real app window (not desktop, taskbar or our own) overlaps <paramref name="area"/>.</summary>
    public static bool IsOccupied(IEnumerable<ForegroundWindow> windows, PixelRect area) =>
        windows.Any(w => !w.IsOwnProcess && !FullscreenRules.ShellClasses.Contains(w.ClassName) && w.Bounds.Intersects(area));
}
