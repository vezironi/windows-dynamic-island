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
}
