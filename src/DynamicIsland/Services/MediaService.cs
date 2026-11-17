using System.IO;
using System.Windows.Threading;
using DynamicIsland.Core;
using Windows.Media.Control;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace DynamicIsland.Services;

/// <summary>Adapter over Windows' Global System Media Transport Controls (Spotify, browsers, ...).</summary>
public sealed class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Session? _session;
    private Dispatcher _ui = null!;
    private DispatcherTimer? _debounce;
    private bool _propsDirty = true;

    // Cached media properties so playback/timeline updates don't re-read the thumbnail.
    private string _title = "", _artist = "";
    private byte[]? _thumb;

    public event Action<MediaInfo?>? MediaChanged;

    public async Task StartAsync(Dispatcher ui)
    {
        _ui = ui;
        _debounce = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, async (_, _) =>
        {
            _debounce!.Stop();
            await PublishAsync();
        }, ui);
        _debounce.Stop();

        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => _ui.BeginInvoke(() => { AttachSession(); Schedule(props: true); });
            AttachSession();
            Schedule(props: true);
        }
        catch (Exception ex) { Log.Error("MediaService.Start", ex); }
    }

    private void AttachSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnProps;
            _session.PlaybackInfoChanged -= OnOther;
            _session.TimelinePropertiesChanged -= OnOther;
        }
        try { _session = _manager?.GetCurrentSession(); }
        catch (Exception ex) { Log.Error("MediaService.GetCurrentSession", ex); _session = null; }
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnProps;
            _session.PlaybackInfoChanged += OnOther;
            _session.TimelinePropertiesChanged += OnOther;
        }
    }

    private void OnProps(Session s, MediaPropertiesChangedEventArgs e) => _ui.BeginInvoke(() => Schedule(props: true));
    private void OnOther(Session s, object e) => _ui.BeginInvoke(() => Schedule(props: false));

    private void Schedule(bool props)
    {
        _propsDirty |= props;
        _debounce?.Stop();
        _debounce?.Start();
    }

    private async Task PublishAsync()
    {
        var s = _session;
        if (s is null) { MediaChanged?.Invoke(null); return; }
        try
        {
            if (_propsDirty)
            {
                _propsDirty = false;
                var p = await s.TryGetMediaPropertiesAsync();
                _title = p?.Title ?? "";
                _artist = p?.Artist ?? "";
                _thumb = p?.Thumbnail is { } t ? await ReadThumbAsync(t) : null;
            }
            if (!ReferenceEquals(s, _session)) return; // session switched while awaiting

            if (string.IsNullOrWhiteSpace(_title)) { MediaChanged?.Invoke(null); return; }

            var pb = s.GetPlaybackInfo();
            var tl = s.GetTimelineProperties();
            var playing = pb?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var duration = tl is null ? TimeSpan.Zero : tl.EndTime - tl.StartTime;
            var position = tl?.Position ?? TimeSpan.Zero;
            var updated = tl?.LastUpdatedTime ?? DateTimeOffset.Now;
            MediaChanged?.Invoke(new MediaInfo(_title, _artist, playing, position, duration, updated, _thumb));
        }
        catch (Exception ex)
        {
            Log.Error("MediaService.Publish", ex);
        }
    }

    private static async Task<byte[]?> ReadThumbAsync(global::Windows.Storage.Streams.IRandomAccessStreamReference reference)
    {
        try
        {
            using var ras = await reference.OpenReadAsync();
            using var stream = ras.AsStreamForRead();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (Exception ex) { Log.Error("MediaService.Thumbnail", ex); return null; }
    }

    public Task PlayPauseAsync() => Run(s => s.TryTogglePlayPauseAsync().AsTask());
    public Task NextAsync() => Run(s => s.TrySkipNextAsync().AsTask());
    public Task PreviousAsync() => Run(s => s.TrySkipPreviousAsync().AsTask());

    private async Task Run(Func<Session, Task<bool>> command)
    {
        if (_session is null) return;
        try { await command(_session); }
        catch (Exception ex) { Log.Error("MediaService.Command", ex); }
    }

    public void Dispose()
    {
        _debounce?.Stop();
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnProps;
            _session.PlaybackInfoChanged -= OnOther;
            _session.TimelinePropertiesChanged -= OnOther;
        }
    }
}
