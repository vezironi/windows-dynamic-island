using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class AudioBandsTests
{
    private const int Rate = 48000;

    private static float[] Sine(double hz, double amplitude, int n = AudioBands.FrameSize) =>
        Enumerable.Range(0, n).Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / Rate))).ToArray();

    [Fact]
    public void Silence_AllZero()
    {
        var bands = AudioBands.Compute(new float[AudioBands.FrameSize], Rate);
        Assert.Equal(AudioBands.BandCount, bands.Length);
        Assert.All(bands, b => Assert.Equal(0, b, 3));
    }

    [Theory]
    [InlineData(80, 0)]     // bass
    [InlineData(300, 1)]    // low-mid
    [InlineData(1000, 2)]   // mid
    [InlineData(4000, 3)]   // treble
    public void Sine_LightsItsOwnBandMost(double hz, int expectedBand)
    {
        var bands = AudioBands.Compute(Sine(hz, 0.5), Rate);
        var loudest = Array.IndexOf(bands, bands.Max());
        Assert.Equal(expectedBand, loudest);
        Assert.InRange(bands[expectedBand], 0.6, 1.0);
    }

    [Fact]
    public void QuietSine_IsLowerThanLoudSine()
    {
        var loud = AudioBands.Compute(Sine(1000, 0.5), Rate)[2];
        var quiet = AudioBands.Compute(Sine(1000, 0.01), Rate)[2];
        Assert.True(quiet < loud - 0.2, $"quiet={quiet} loud={loud}");
    }

    [Fact]
    public void ShortInput_ZeroPadded_NoThrow()
    {
        var bands = AudioBands.Compute(Sine(1000, 0.5, 100), Rate);
        Assert.Equal(AudioBands.BandCount, bands.Length);
    }

    [Fact]
    public void Smoother_RisesFast_FallsSlow()
    {
        var s = new BarSmoother(1);
        s.Update([1.0], 1.0 / 60);
        var afterOneRise = s.Values[0];
        Assert.True(afterOneRise > 0.3, $"rise {afterOneRise}");

        for (var i = 0; i < 30; i++) s.Update([1.0], 1.0 / 60);
        var top = s.Values[0];
        s.Update([0.0], 1.0 / 60);
        var drop = top - s.Values[0];
        Assert.True(drop < afterOneRise, $"drop {drop} should be gentler than rise {afterOneRise}");
        Assert.True(drop > 0);
    }

    [Fact]
    public void Smoother_ClampsToUnitRange()
    {
        var s = new BarSmoother(2);
        s.Update([5.0, -3.0], 1.0);
        Assert.InRange(s.Values[0], 0, 1);
        Assert.InRange(s.Values[1], 0, 1);
    }
}
