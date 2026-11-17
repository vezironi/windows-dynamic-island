using System.Globalization;

namespace DynamicIsland.Core;

/// <summary>
/// Builds the notch outline: a body hanging from the screen's top edge whose top corners
/// flare outward (concave) into the edge, and whose bottom corners are rounded.
/// </summary>
public static class NotchGeometry
{
    /// <returns>WPF path mini-language; total width = bodyWidth + 2*flare, top edge at y=0.</returns>
    public static string BuildPathData(double bodyWidth, double height, double flare, double bottomRadius)
    {
        bodyWidth = Math.Max(0, bodyWidth);
        height = Math.Max(0, height);
        var f = Math.Clamp(flare, 0, height);
        var r = Math.Max(0, Math.Min(bottomRadius, Math.Min(height - f, bodyWidth / 2)));
        var left = f;
        var right = f + bodyWidth;
        var total = bodyWidth + 2 * f;

        return string.Create(CultureInfo.InvariantCulture,
            $"M0,0 Q{N(left)},0 {N(left)},{N(f)} " +
            $"L{N(left)},{N(height - r)} A{N(r)},{N(r)} 0 0 0 {N(left + r)},{N(height)} " +
            $"L{N(right - r)},{N(height)} A{N(r)},{N(r)} 0 0 0 {N(right)},{N(height - r)} " +
            $"L{N(right)},{N(f)} Q{N(right)},0 {N(total)},0 Z");
    }

    private static string N(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
