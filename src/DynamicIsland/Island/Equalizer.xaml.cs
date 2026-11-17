using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicIsland.Core;

namespace DynamicIsland.Island;

/// <summary>
/// Four bars driven by the real audio level (bass → treble), like the iPhone's Dynamic Island.
/// Falls back to a decorative bounce when no audio level source is available.
/// </summary>
public partial class Equalizer : UserControl
{
    private const double MinScale = 0.18;
    private static readonly double[] FallbackDurations = [0.42, 0.55, 0.37, 0.61];

    private readonly BarSmoother _smoother = new(AudioBands.BandCount);
    private readonly ScaleTransform[] _scales;
    private bool _playing;
    private bool _rendering;
    private bool _fallbackRunning;
    private TimeSpan _last;

    public Equalizer()
    {
        InitializeComponent();
        _scales = new[] { B1, B2, B3, B4 }.Select(b => (ScaleTransform)b.RenderTransform).ToArray();
        IsVisibleChanged += (_, _) => Sync();
    }

    /// <summary>Returns the latest band levels, or null when capture is unavailable.</summary>
    public Func<double[]?>? LevelSource { get; set; }

    /// <summary>Freeze the bars at given levels (used by --screenshots).</summary>
    internal void ShowStaticLevels(double[] levels)
    {
        for (var i = 0; i < _scales.Length; i++)
            _scales[i].ScaleY = MinScale + (1 - MinScale) * Math.Clamp(i < levels.Length ? levels[i] : 0, 0, 1);
    }

    public void SetPlaying(bool playing)
    {
        if (playing == _playing) return;
        _playing = playing;
        Sync();
    }

    private void Sync()
    {
        var active = _playing && IsVisible;
        if (active && !_rendering)
        {
            _rendering = true;
            _last = TimeSpan.Zero;
            CompositionTarget.Rendering += OnFrame;
        }
        else if (!active && _rendering)
        {
            _rendering = false;
            CompositionTarget.Rendering -= OnFrame;
            StopFallback();
            Array.Clear(_smoother.Values);
            foreach (var s in _scales) s.ScaleY = MinScale + 0.12;
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (t == _last) return;
        var dt = _last == TimeSpan.Zero ? 1.0 / 60 : Math.Min((t - _last).TotalSeconds, 0.1);
        _last = t;

        var levels = LevelSource?.Invoke();
        if (levels is null)
        {
            StartFallback();
            return;
        }
        StopFallback();
        _smoother.Update(levels, dt);
        for (var i = 0; i < _scales.Length; i++)
            _scales[i].ScaleY = MinScale + (1 - MinScale) * _smoother.Values[i];
    }

    private void StartFallback()
    {
        if (_fallbackRunning) return;
        _fallbackRunning = true;
        for (var i = 0; i < _scales.Length; i++)
        {
            var a = new DoubleAnimation(0.25, 1.0, TimeSpan.FromSeconds(FallbackDurations[i]))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }
    }

    private void StopFallback()
    {
        if (!_fallbackRunning) return;
        _fallbackRunning = false;
        foreach (var s in _scales) s.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }
}
