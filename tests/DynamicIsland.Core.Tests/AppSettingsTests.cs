using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "di-tests-" + Guid.NewGuid().ToString("N"));
    private string PathIn => System.IO.Path.Combine(_dir, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Settings_Missing_Defaults() => Assert.Equal(new AppSettings(), AppSettings.Load(PathIn));

    [Fact]
    public void Settings_Corrupt_Defaults()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathIn)!);
        File.WriteAllText(PathIn, "{not json");
        Assert.Equal(new AppSettings(), AppSettings.Load(PathIn));
    }

    [Fact]
    public void Settings_Defaults_AreAsSpecified()
    {
        var d = new AppSettings();
        Assert.True(d.StartWithWindows);
        Assert.True(d.PlaySound);
        Assert.False(d.OnboardingDone);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var s = new AppSettings(StartWithWindows: false, PlaySound: false, OnboardingDone: true);
        s.Save(PathIn);
        Assert.Equal(s, AppSettings.Load(PathIn));
    }
}
