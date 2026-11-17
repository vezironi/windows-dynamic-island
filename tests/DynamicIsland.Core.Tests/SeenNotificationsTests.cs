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
    public void ManyPresent_NoReplay()
    {
        var s = new SeenNotifications();
        var ids = Enumerable.Range(0, 600).Select(i => i.ToString()).ToList();
        s.Seed(ids);
        Assert.All(ids, id => Assert.False(s.MarkIfNew(id)));
        s.Retain(ids);
        Assert.All(ids, id => Assert.False(s.MarkIfNew(id)));
    }

    [Fact]
    public void Retain_ForgetsIdsNoLongerPresent()
    {
        var s = new SeenNotifications();
        s.MarkIfNew("a");
        s.MarkIfNew("b");
        s.Retain(["b"]);
        Assert.True(s.MarkIfNew("a"));
        Assert.False(s.MarkIfNew("b"));
    }
}
