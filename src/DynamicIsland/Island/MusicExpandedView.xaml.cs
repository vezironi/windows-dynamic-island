using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Services;

namespace DynamicIsland.Island;

public partial class MusicExpandedView : UserControl
{
    private const string PlayGlyph = "", PauseGlyph = "";
    private readonly DispatcherTimer _progressTimer;
    private MediaInfo? _media;
    private Func<DateTimeOffset> _now = () => DateTimeOffset.Now;

    public MediaService? Media { get; set; }

    public MusicExpandedView()
    {
        InitializeComponent();
        PrevButton.Click += async (_, _) => { if (Media is not null) await Media.PreviousAsync(); };
        NextButton.Click += async (_, _) => { if (Media is not null) await Media.NextAsync(); };
        PlayButton.Click += async (_, _) => { if (Media is not null) await Media.PlayPauseAsync(); };
        _progressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Render, (_, _) => UpdateProgress(), Dispatcher);
        _progressTimer.Stop();
        IsVisibleChanged += (_, _) => SyncTimer();
        Track.SizeChanged += (_, _) => UpdateProgress();
    }

    public void Update(MediaInfo m, Func<DateTimeOffset> now)
    {
        _media = m;
        _now = now;
        TitleText.Text = m.Title;
        ArtistText.Text = m.Artist;
        var img = Ui.ImageFromBytes(m.Thumbnail);
        Cover.Background = img is null ? null : new ImageBrush(img) { Stretch = Stretch.UniformToFill };
        CoverGlyph.Visibility = img is null ? Visibility.Visible : Visibility.Collapsed;
        PlayButton.Content = m.IsPlaying ? PauseGlyph : PlayGlyph;
        UpdateProgress();
        SyncTimer();
    }

    private void SyncTimer()
    {
        if (IsVisible && _media is { IsPlaying: true }) _progressTimer.Start();
        else _progressTimer.Stop();
    }

    private void UpdateProgress()
    {
        if (_media is not { } m) return;
        var pos = m.Position + (m.IsPlaying ? _now() - m.PositionUpdatedAt : TimeSpan.Zero);
        if (m.Duration > TimeSpan.Zero && pos > m.Duration) pos = m.Duration;
        var hasDuration = m.Duration > TimeSpan.Zero;
        ElapsedText.Text = hasDuration ? Ui.FormatTime(pos) : "";
        DurationText.Text = hasDuration ? Ui.FormatTime(m.Duration) : "";
        var frac = hasDuration ? Math.Clamp(pos / m.Duration, 0, 1) : 0;
        Fill.Width = Track.ActualWidth * frac;
        Track.Visibility = hasDuration ? Visibility.Visible : Visibility.Hidden;
    }
}
