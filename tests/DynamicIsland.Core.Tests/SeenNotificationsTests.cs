using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class SeenNotificationsTests
{
    [Fact]
    public void Seeded_IdsAreNotNew()
    {
        var s = new SeenNotifications();
        s.Seed(["1", "2"]);
        Assert.False(s.MarkIfNew("1"));
        Assert.False(s.MarkIfNew("2"));
        Assert.True(s.MarkIfNew("3"));
    }

    [Fact]
    public void SameIdTwice_OnlyFirstIsNew()
    {
        var s = new SeenNotifications();
        Assert.True(s.MarkIfNew("9"));
        Assert.False(s.MarkIfNew("9"));
    }

    [Fact]
    public void Capacity_EvictsOldest()
    {
        var s = new SeenNotifications();
        for (var i = 0; i < 501; i++) s.MarkIfNew(i.ToString());
        Assert.True(s.MarkIfNew("0"));
        Assert.False(s.MarkIfNew("500"));
    }
}
