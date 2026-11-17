using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class FullscreenRulesTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);
    private static ForegroundWindow Win(string cls, PixelRect bounds, bool maximized = false, bool own = false) =>
        new(cls, bounds, maximized, own);

    private static bool Check(ForegroundWindow? fg, UserNotificationState q = UserNotificationState.AcceptsNotifications) =>
        FullscreenRules.IsFullscreen(q, fg, Monitor);

    [Fact]
    public void D3DFullscreen_True() => Assert.True(Check(null, UserNotificationState.RunningD3DFullScreen));

    [Fact]
    public void PresentationMode_True() => Assert.True(Check(null, UserNotificationState.PresentationMode));

    [Fact]
    public void Busy_True() => Assert.True(Check(null, UserNotificationState.Busy));

    [Fact]
    public void BorderlessGameCoveringMonitor_True() => Assert.True(Check(Win("UnityWndClass", Monitor)));

    [Fact]
    public void BrowserF11_True() => Assert.True(Check(Win("Chrome_WidgetWin_1", Monitor)));

    [Fact]
    public void MaximizedWithAutoHideTaskbar_False() =>
        Assert.False(Check(Win("Chrome_WidgetWin_1", Monitor, maximized: true)));

    [Fact]
    public void NormalMaximizedWindow_False() =>
        Assert.False(Check(Win("Notepad", new PixelRect(0, 0, 1920, 1032), maximized: true)));

    [Fact]
    public void Desktop_False() => Assert.False(Check(Win("WorkerW", Monitor)));

    [Fact]
    public void OwnWindow_False() => Assert.False(Check(Win("HwndWrapper", Monitor, own: true)));

    [Fact]
    public void NullForeground_AcceptsNotifications_False() => Assert.False(Check(null));

    [Fact]
    public void SecondMonitorWindowCoveringItsMonitor_True() =>
        Assert.True(FullscreenRules.IsFullscreen(UserNotificationState.AcceptsNotifications,
            Win("UnityWndClass", new PixelRect(1920, 0, 4480, 1440)), new PixelRect(1920, 0, 4480, 1440)));

    // ---- Primary monitor covered while focus is on another monitor ----

    private static readonly PixelRect Primary = new(0, 0, 2560, 1440);

    [Fact]
    public void PrimaryCovered_GameOnTop_True() =>
        Assert.True(FullscreenRules.IsMonitorCovered([Win("UnityWndClass", Primary)], Primary));

    [Fact]
    public void PrimaryCovered_NormalWindowOnTop_False() =>
        Assert.False(FullscreenRules.IsMonitorCovered(
            [Win("Notepad", new PixelRect(100, 100, 900, 700)), Win("UnityWndClass", Primary)], Primary));

    [Fact]
    public void PrimaryCovered_SkipsTaskbarAndOwnWindow() =>
        Assert.True(FullscreenRules.IsMonitorCovered(
            [Win("Shell_TrayWnd", new PixelRect(0, 1392, 2560, 1440)), Win("HwndWrapper", new PixelRect(1050, 0, 1510, 140), own: true),
             Win("Chrome_WidgetWin_1", Primary)], Primary));

    [Fact]
    public void PrimaryCovered_MaximizedBrowser_False() =>
        Assert.False(FullscreenRules.IsMonitorCovered([Win("Chrome_WidgetWin_1", Primary, maximized: true)], Primary));

    [Fact]
    public void PrimaryCovered_WindowsOnOtherMonitorIgnored() =>
        Assert.True(FullscreenRules.IsMonitorCovered(
            [Win("Discord", new PixelRect(-2560, 0, -1200, 900)), Win("UnityWndClass", Primary)], Primary));

    [Fact]
    public void PrimaryCovered_Empty_False() =>
        Assert.False(FullscreenRules.IsMonitorCovered([], Primary));

    // ---- Is anything under the island (top-center of the primary monitor)? ----

    private static readonly PixelRect IslandArea = new(1150, 0, 1410, 40);

    [Fact]
    public void TopArea_Empty_Free() => Assert.False(TopAreaRules.IsOccupied([], IslandArea));

    [Fact]
    public void TopArea_DesktopTaskbarAndOwn_Free() =>
        Assert.False(TopAreaRules.IsOccupied(
            [Win("WorkerW", Primary), Win("Progman", Primary), Win("Shell_TrayWnd", new PixelRect(0, 0, 2560, 48)),
             Win("HwndWrapper", new PixelRect(1050, 0, 1510, 140), own: true)], IslandArea));

    [Fact]
    public void TopArea_MaximizedBrowser_Occupied() =>
        Assert.True(TopAreaRules.IsOccupied([Win("Chrome_WidgetWin_1", Primary, maximized: true)], IslandArea));

    [Fact]
    public void TopArea_SmallWindowTouchingTop_Occupied() =>
        Assert.True(TopAreaRules.IsOccupied([Win("Chrome_WidgetWin_1", new PixelRect(1000, 0, 1800, 900))], IslandArea));

    [Fact]
    public void TopArea_WindowElsewhere_Free() =>
        Assert.False(TopAreaRules.IsOccupied(
            [Win("Notepad", new PixelRect(100, 300, 900, 900)), Win("Discord", new PixelRect(-2560, 0, -100, 1400))], IslandArea));
}
