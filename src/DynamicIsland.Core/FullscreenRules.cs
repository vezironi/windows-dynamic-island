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
    public bool Contains(PixelRect o) =>
        Left <= o.Left && Top <= o.Top && Right >= o.Right && Bottom >= o.Bottom;
}

/// <param name="Bounds">DWM extended-frame bounds of the window, in physical pixels.</param>
public sealed record ForegroundWindow(string ClassName, PixelRect Bounds, bool IsMaximized, bool IsOwnProcess);

public static class FullscreenRules
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

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
