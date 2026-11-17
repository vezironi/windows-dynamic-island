namespace DynamicIsland.Core;

/// <summary>
/// All show / hide / expand / queue decisions for the island. Pure: time is passed in,
/// nothing runs on its own — the host calls <see cref="Tick"/> periodically.
/// </summary>
public sealed class IslandStateMachine
{
    private readonly IslandTimings _t;
    private readonly Queue<IslandNotification> _queue = new();

    private DateTimeOffset? _pausedAt;          // when playback went playing → paused
    private bool _pointerOver;
    private DateTimeOffset? _hoverStart;
    private bool _expanded;
    private DateTimeOffset? _collapseAt;
    private DateTimeOffset? _notifDeadline;     // null while frozen by hover
    private TimeSpan _notifRemaining;
    private bool _fullscreen;

    public IslandStateMachine(IslandTimings timings) => _t = timings;

    public IslandMode Mode { get; private set; } = IslandMode.Hidden;
    public bool IsFullscreen => _fullscreen;
    public MediaInfo? Media { get; private set; }
    public IslandNotification? CurrentNotification { get; private set; }

    /// <summary>Raised only when Mode, Media or CurrentNotification actually changes.</summary>
    public event Action? Changed;

    public void OnMediaChanged(MediaInfo? media, DateTimeOffset now) => Apply(now, () =>
    {
        var wasPlaying = Media?.IsPlaying ?? false;
        var hadMedia = Media is not null;
        Media = media;
        if (media is null)
        {
            _pausedAt = null;
            _expanded = false;
            _collapseAt = null;
        }
        else if (media.IsPlaying)
        {
            _pausedAt = null;
        }
        else if (wasPlaying)
        {
            _pausedAt = now;
        }
        else if (!hadMedia)
        {
            // First seen already paused (e.g. app start with Spotify paused): don't pop up.
            _pausedAt = DateTimeOffset.MinValue;
        }
    });

    public void OnNotification(IslandNotification n, DateTimeOffset now) => Apply(now, () =>
    {
        if (_fullscreen) return;
        if (CurrentNotification is null) Begin(n, now);
        else _queue.Enqueue(n);
    });

    public void OnPointerEnter(DateTimeOffset now) => Apply(now, () =>
    {
        _pointerOver = true;
        _hoverStart = now;
        _collapseAt = null;
        if (CurrentNotification is not null && _notifDeadline is { } d)
        {
            _notifRemaining = d > now ? d - now : TimeSpan.Zero;
            _notifDeadline = null;
        }
    });

    public void OnPointerLeave(DateTimeOffset now) => Apply(now, () =>
    {
        _pointerOver = false;
        _hoverStart = null;
        if (_expanded) _collapseAt = now + _t.CollapseDelay;
        if (CurrentNotification is not null && _notifDeadline is null)
            _notifDeadline = now + _notifRemaining;
    });

    /// <summary>Returns the notification to activate if one was clicked; otherwise expands music.</summary>
    public IslandNotification? OnClick(DateTimeOffset now)
    {
        IslandNotification? clicked = null;
        Apply(now, () =>
        {
            if (Mode == IslandMode.Notification && CurrentNotification is not null)
            {
                clicked = CurrentNotification;
                Advance(now);
                // The island "closes" back to its base state; don't let the hover that led to
                // the click expand the music view until the pointer leaves and comes back.
                _expanded = false;
                _collapseAt = null;
                _hoverStart = null;
            }
            else if (Mode == IslandMode.MusicCompact)
            {
                _expanded = true;
                _collapseAt = null;
            }
        });
        return clicked;
    }

    public void OnFullscreenChanged(bool isFullscreen, DateTimeOffset now) => Apply(now, () =>
    {
        _fullscreen = isFullscreen;
        if (isFullscreen)
        {
            _queue.Clear();
            CurrentNotification = null;
            _notifDeadline = null;
            _expanded = false;
            _collapseAt = null;
        }
    });

    public void Tick(DateTimeOffset now) => Apply(now, () => { });

    private void Begin(IslandNotification n, DateTimeOffset now)
    {
        CurrentNotification = n;
        if (_pointerOver)
        {
            _notifDeadline = null;
            _notifRemaining = _t.NotificationDuration;
        }
        else
        {
            _notifDeadline = now + _t.NotificationDuration;
        }
    }

    private void Advance(DateTimeOffset now)
    {
        CurrentNotification = null;
        _notifDeadline = null;
        if (_queue.TryDequeue(out var next)) Begin(next, now);
    }

    private void Apply(DateTimeOffset now, Action mutate)
    {
        var (mode, media, notif) = (Mode, Media, CurrentNotification);
        mutate();
        RunTimers(now);
        Mode = Compute(now);
        if (mode != Mode || !ReferenceEquals(media, Media) || !ReferenceEquals(notif, CurrentNotification))
            Changed?.Invoke();
    }

    private void RunTimers(DateTimeOffset now)
    {
        if (CurrentNotification is not null && _notifDeadline is { } d && now >= d)
            Advance(now);

        if (_pointerOver && !_expanded && _hoverStart is { } h && now - h >= _t.HoverDelay)
            _expanded = true;

        if (_collapseAt is { } c && now >= c)
        {
            _expanded = false;
            _collapseAt = null;
        }
    }

    private IslandMode Compute(DateTimeOffset now)
    {
        if (_fullscreen) return IslandMode.Hidden;
        if (CurrentNotification is not null) return IslandMode.Notification;
        if (!MusicVisible(now)) return IslandMode.Hidden;
        return _expanded ? IslandMode.MusicExpanded : IslandMode.MusicCompact;
    }

    private bool MusicVisible(DateTimeOffset now)
    {
        if (Media is null) return false;
        if (Media.IsPlaying || _pointerOver || _expanded) return true;
        return _pausedAt is { } p && p != DateTimeOffset.MinValue && now - p < _t.PausedHide;
    }
}
