using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class SpringTests
{
    [Fact]
    public void Spring_ConvergesWithin1s()
    {
        var s = new Spring(0) { Target = 100 };
        for (var i = 0; i < 60; i++) s.Step(1.0 / 60);
        Assert.True(s.IsSettled);
        Assert.InRange(s.Value, 99.5, 100.5);
    }

    [Fact]
    public void Spring_Overshoots()
    {
        var s = new Spring(0) { Target = 100 };
        var max = 0.0;
        for (var i = 0; i < 60; i++) { s.Step(1.0 / 60); max = Math.Max(max, s.Value); }
        Assert.InRange(max, 100.0001, 110);
    }

    [Fact]
    public void Spring_LargeDt_IsStable()
    {
        var s = new Spring(0) { Target = 100 };
        s.Step(0.5);
        Assert.True(double.IsFinite(s.Value));
        Assert.True(s.Value < 200);
    }

    [Fact]
    public void Spring_AtTarget_IsSettled()
    {
        var s = new Spring(42) { Target = 42 };
        Assert.True(s.IsSettled);
    }
}
