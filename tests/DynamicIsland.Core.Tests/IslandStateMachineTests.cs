using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class IslandStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(double ms) => T0.AddMilliseconds(ms);

    private static MediaInfo Playing(string title = "Song") =>
        new(title, "Artist", true, TimeSpan.Zero, TimeSpan.FromMinutes(3), T0, null);
    private static MediaInfo Paused(string title = "Song") => Playing(title) with { IsPlaying = false };
    private static IslandNotification Notif(string id) => new(id, "WhatsApp", "aumid!" + id, "Title " + id, "Body", null);

    private static IslandStateMachine New() => new(IslandTimings.Default);

    [Fact]
    public void StartsHidden() => Assert.Equal(IslandMode.Hidden, New().Mode);

    [Fact]
    public void PlayingMedia_ShowsCompact()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
        Assert.Equal("Song", sm.Media!.Title);
    }

    [Fact]
    public void Hover_ExpandsOnlyAfterDelay()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnPointerEnter(T0);
        sm.Tick(At(200));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
        sm.Tick(At(260));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
    }

    [Fact]
    public void HoverLeave_CollapsesAfterDelay()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnPointerEnter(T0);
        sm.Tick(At(300));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
        sm.OnPointerLeave(At(1000));
        sm.Tick(At(1400));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
        sm.Tick(At(1510));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
    }

    [Fact]
    public void Click_OnCompact_ExpandsImmediately_ReturnsNull()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnPointerEnter(T0);
        var clicked = sm.OnClick(At(10));
        Assert.Null(clicked);
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
    }

    [Fact]
    public void Paused_HidesAfter5s()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnMediaChanged(Paused(), T0);
        sm.Tick(At(4900));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
        sm.Tick(At(5100));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
    }

    [Fact]
    public void FirstSeenPaused_DoesNotShow()
    {
        var sm = New();
        sm.OnMediaChanged(Paused(), T0);
        Assert.Equal(IslandMode.Hidden, sm.Mode);
    }

    [Fact]
    public void Paused_ThenResumed_DoesNotHide()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnMediaChanged(Paused(), T0);
        sm.OnMediaChanged(Playing(), At(3000));
        sm.Tick(At(9000));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
    }

    [Fact]
    public void Paused_WhilePointerOver_DoesNotHide_UntilLeave()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnMediaChanged(Paused(), T0);
        sm.OnPointerEnter(At(1000));
        sm.Tick(At(8000));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
        sm.OnPointerLeave(At(8000));
        sm.Tick(At(8600));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
    }

    [Fact]
    public void MediaNull_WhileExpanded_HidesImmediately_AndClearsMedia()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnPointerEnter(T0);
        sm.Tick(At(300));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
        sm.OnMediaChanged(null, At(400));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
        Assert.Null(sm.Media);
    }

    [Fact]
    public void Notification_ShowsThenReturnsToMusic()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnNotification(Notif("1"), T0);
        Assert.Equal(IslandMode.Notification, sm.Mode);
        Assert.Equal("1", sm.CurrentNotification!.Id);
        sm.Tick(At(4900));
        Assert.Equal(IslandMode.Notification, sm.Mode);
        sm.Tick(At(5100));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);   // peek (4s) is over by now
        Assert.Null(sm.CurrentNotification);
    }

    [Fact]
    public void Notification_WithNoMedia_ReturnsToHidden()
    {
        var sm = New();
        sm.OnNotification(Notif("1"), T0);
        Assert.Equal(IslandMode.Notification, sm.Mode);
        sm.Tick(At(5100));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
    }

    [Fact]
    public void Notifications_AreQueued_FIFO()
    {
        var sm = New();
        sm.OnNotification(Notif("1"), T0);
        sm.OnNotification(Notif("2"), T0);
        Assert.Equal("1", sm.CurrentNotification!.Id);
        sm.Tick(At(5100));
        Assert.Equal("2", sm.CurrentNotification!.Id);
        sm.Tick(At(10200));
        Assert.Null(sm.CurrentNotification);
        Assert.Equal(IslandMode.Hidden, sm.Mode);
    }

    [Fact]
    public void Notification_HoverFreezesTimer()
    {
        var sm = New();
        sm.OnNotification(Notif("1"), T0);
        sm.OnPointerEnter(At(2000));
        sm.Tick(At(20000));
        Assert.Equal(IslandMode.Notification, sm.Mode);
        sm.OnPointerLeave(At(20000));
        sm.Tick(At(22900));
        Assert.Equal(IslandMode.Notification, sm.Mode);
        sm.Tick(At(23100));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
    }

    [Fact]
    public void Click_OnNotification_ReturnsIt_AndDismisses()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnNotification(Notif("1"), T0);
        var clicked = sm.OnClick(At(1000));
        Assert.Equal("1", clicked!.Id);
        Assert.Null(sm.CurrentNotification);
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
    }

    [Fact]
    public void Click_OnNotification_AfterHover_DoesNotExpandMusic()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnNotification(Notif("1"), T0);
        sm.OnPointerEnter(At(100));
        sm.Tick(At(500));
        Assert.Equal(IslandMode.Notification, sm.Mode);
        sm.OnClick(At(600));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
        sm.Tick(At(2000));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
    }

    [Fact]
    public void IsFullscreen_ReflectsLastSignal()
    {
        var sm = New();
        Assert.False(sm.IsFullscreen);
        sm.OnFullscreenChanged(true, T0);
        Assert.True(sm.IsFullscreen);
        sm.OnFullscreenChanged(false, T0);
        Assert.False(sm.IsFullscreen);
    }

    [Fact]
    public void Fullscreen_HidesEverything_DropsQueue_IgnoresNew()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.OnNotification(Notif("1"), T0);
        sm.OnNotification(Notif("q"), T0);
        sm.OnFullscreenChanged(true, At(100));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
        sm.OnNotification(Notif("2"), At(200));
        sm.OnMediaChanged(Playing("Other"), At(300));
        sm.Tick(At(400));
        Assert.Equal(IslandMode.Hidden, sm.Mode);
        sm.OnFullscreenChanged(false, At(1000));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
        Assert.Null(sm.CurrentNotification);
        Assert.Equal("Other", sm.Media!.Title);
        sm.Tick(At(20000));
        Assert.Null(sm.CurrentNotification);
    }

    // ---- Tucked: thin line at the top so browser tabs stay clickable ----

    [Fact]
    public void PlayStart_PeeksThenTucks()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.Tick(At(3900));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
        sm.Tick(At(4100));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
    }

    [Fact]
    public void TrackChange_PeeksAgain()
    {
        var sm = New();
        sm.OnMediaChanged(Playing("A"), T0);
        sm.Tick(At(5000));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
        sm.OnMediaChanged(Playing("B"), At(6000));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
        sm.Tick(At(10100));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
    }

    [Fact]
    public void SameTrackUpdate_DoesNotPeek()
    {
        var sm = New();
        sm.OnMediaChanged(Playing("A"), T0);
        sm.Tick(At(5000));
        sm.OnMediaChanged(Playing("A") with { Position = TimeSpan.FromSeconds(42) }, At(6000));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
    }

    [Fact]
    public void Resume_Peeks()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.Tick(At(4100));
        sm.OnMediaChanged(Paused(), At(4200));
        sm.OnMediaChanged(Playing(), At(4500));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
    }

    [Fact]
    public void HoverOnTucked_Expands_LeaveReturnsToTucked()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.Tick(At(4100));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
        sm.OnPointerEnter(At(5000));
        sm.Tick(At(5300));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
        sm.OnPointerLeave(At(6000));
        sm.Tick(At(6600));
        Assert.Equal(IslandMode.MusicTucked, sm.Mode);
    }

    [Fact]
    public void ClickOnTucked_Expands()
    {
        var sm = New();
        sm.OnMediaChanged(Playing(), T0);
        sm.Tick(At(4100));
        sm.OnPointerEnter(At(5000));
        Assert.Null(sm.OnClick(At(5050)));
        Assert.Equal(IslandMode.MusicExpanded, sm.Mode);
    }

    [Fact]
    public void FirstSeenPlaying_AfterPausedStart_Peeks()
    {
        var sm = New();
        sm.OnMediaChanged(Paused(), T0);
        sm.OnMediaChanged(Playing(), At(1000));
        Assert.Equal(IslandMode.MusicCompact, sm.Mode);
    }

    [Fact]
    public void Changed_RaisedOnlyOnActualChange()
    {
        var sm = New();
        var count = 0;
        sm.Changed += () => count++;
        sm.OnMediaChanged(Playing(), T0);
        Assert.Equal(1, count);
        sm.Tick(At(10));
        sm.Tick(At(20));
        Assert.Equal(1, count);
    }
}
