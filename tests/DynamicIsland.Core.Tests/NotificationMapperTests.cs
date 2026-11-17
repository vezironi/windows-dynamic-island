using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class NotificationMapperTests
{
    private const string Own = "DynamicIsland_abc!DynamicIsland";

    [Fact]
    public void Map_TitleAndBody()
    {
        var n = NotificationMapper.TryMap("7", "WhatsApp", "wa!App", ["  Ahmet ", "akşam geliyor musun?", "", "ikinci satır"], null, Own);
        Assert.NotNull(n);
        Assert.Equal("7", n.Id);
        Assert.Equal("WhatsApp", n.AppName);
        Assert.Equal("wa!App", n.AppUserModelId);
        Assert.Equal("Ahmet", n.Title);
        Assert.Equal("akşam geliyor musun?\nikinci satır", n.Body);
    }

    [Fact]
    public void Map_SingleText_EmptyBody()
    {
        var n = NotificationMapper.TryMap("1", "Discord", "d!App", ["Yeni mesaj"], null, Own);
        Assert.Equal("Yeni mesaj", n!.Title);
        Assert.Equal("", n.Body);
    }

    [Fact]
    public void Map_AllBlank_Null() =>
        Assert.Null(NotificationMapper.TryMap("1", "X", "x!App", ["", "   "], null, Own));

    [Fact]
    public void Map_NoTexts_Null() =>
        Assert.Null(NotificationMapper.TryMap("1", "X", "x!App", [], null, Own));

    [Fact]
    public void Map_OwnApp_Null() =>
        Assert.Null(NotificationMapper.TryMap("1", "Dynamic Island", Own, ["hi"], null, Own));

    [Fact]
    public void Map_BlankAppName_Fallback() =>
        Assert.Equal("Bildirim", NotificationMapper.TryMap("1", " ", "x!App", ["hi"], null, Own)!.AppName);
}
