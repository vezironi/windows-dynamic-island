using System.Globalization;
using System.Text.RegularExpressions;
using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class NotchGeometryTests
{
    private static IEnumerable<string> Numbers(string path) =>
        Regex.Matches(path, @"-?\d+(\.\d+)?").Select(m => m.Value);

    [Fact]
    public void Notch_StartsAtOriginAndClosesAtFullWidth()
    {
        var p = NotchGeometry.BuildPathData(200, 34, 10, 17);
        Assert.StartsWith("M0,0", p);
        Assert.Contains("220,0", p);
        Assert.EndsWith("Z", p);
    }

    [Fact]
    public void Notch_UsesInvariantCulture_UnderTurkish()
    {
        var old = CultureInfo.CurrentCulture;
        string invariant;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            invariant = NotchGeometry.BuildPathData(200.5, 34, 10, 17);
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var turkish = NotchGeometry.BuildPathData(200.5, 34, 10, 17);
            Assert.Equal(invariant, turkish);
            Assert.Contains("210.5,", turkish);
            foreach (var n in Numbers(turkish))
                Assert.True(double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out _), n);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Notch_ClampsRadius()
    {
        var p = NotchGeometry.BuildPathData(20, 15, 10, 17);
        Assert.DoesNotContain(Numbers(p), n => n.StartsWith('-'));
    }

    [Fact]
    public void Notch_ZeroHeight_NoNegatives()
    {
        var p = NotchGeometry.BuildPathData(120, 0, 10, 17);
        Assert.DoesNotContain(Numbers(p), n => n.StartsWith('-'));
    }
}
