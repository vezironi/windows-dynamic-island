using System.Numerics;

namespace DynamicIsland.Core;

/// <summary>
/// Turns a frame of mono audio into 4 bar levels (bass, low-mid, mid, treble) in 0..1,
/// using a Hann-windowed FFT. Treble gets a small boost because music carries less energy there.
/// </summary>
public static class AudioBands
{
    public const int FrameSize = 1024;
    public const int BandCount = 4;

    private static readonly (double Lo, double Hi, double GainDb)[] Bands =
    [
        (40, 150, 0),
        (150, 500, 0),
        (500, 2000, 4),
        (2000, 8000, 10),
    ];

    private const double FloorDb = -50, RangeDb = 45;
    private static readonly double[] Window = Enumerable.Range(0, FrameSize)
        .Select(i => 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FrameSize - 1)))).ToArray();

    /// <param name="samples">Mono samples in -1..1; shorter input is zero-padded, longer is truncated.</param>
    public static double[] Compute(ReadOnlySpan<float> samples, int sampleRate)
    {
        var buf = new Complex[FrameSize];
        var n = Math.Min(samples.Length, FrameSize);
        for (var i = 0; i < n; i++) buf[i] = new Complex(samples[i] * Window[i], 0);
        Fft(buf);

        var binHz = (double)sampleRate / FrameSize;
        var result = new double[BandCount];
        for (var b = 0; b < BandCount; b++)
        {
            var (lo, hi, gain) = Bands[b];
            var first = Math.Max(1, (int)Math.Ceiling(lo / binHz));
            var last = Math.Min(FrameSize / 2 - 1, (int)Math.Floor(hi / binHz));
            var peak = 0.0;
            for (var k = first; k <= last; k++) peak = Math.Max(peak, buf[k].Magnitude);
            var mag = peak * 4 / FrameSize;   // Hann sum = N/2, one-sided ×2 → amplitude of a sine
            if (mag <= 1e-9) continue;
            var db = 20 * Math.Log10(mag) + gain;
            result[b] = Math.Clamp((db - FloorDb) / RangeDb, 0, 1);
        }
        return result;
    }

    /// <summary>In-place iterative radix-2 FFT. Length must be a power of two.</summary>
    private static void Fft(Complex[] a)
    {
        var n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (var len = 2; len <= n; len <<= 1)
        {
            var ang = -2 * Math.PI / len;
            var wl = new Complex(Math.Cos(ang), Math.Sin(ang));
            for (var i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (var k = 0; k < len / 2; k++)
                {
                    var u = a[i + k];
                    var v = a[i + k + len / 2] * w;
                    a[i + k] = u + v;
                    a[i + k + len / 2] = u - v;
                    w *= wl;
                }
            }
        }
    }
}

/// <summary>Attack/release smoothing for bar levels: rises fast, falls gently.</summary>
public sealed class BarSmoother
{
    private const double AttackPerSec = 30, ReleasePerSec = 6;

    public BarSmoother(int count) => Values = new double[count];

    public double[] Values { get; }

    public void Update(IReadOnlyList<double> targets, double dtSeconds)
    {
        for (var i = 0; i < Values.Length; i++)
        {
            var target = Math.Clamp(i < targets.Count ? targets[i] : 0, 0, 1);
            var rate = target > Values[i] ? AttackPerSec : ReleasePerSec;
            var alpha = 1 - Math.Exp(-rate * Math.Max(0, dtSeconds));
            Values[i] = Math.Clamp(Values[i] + (target - Values[i]) * alpha, 0, 1);
        }
    }
}
