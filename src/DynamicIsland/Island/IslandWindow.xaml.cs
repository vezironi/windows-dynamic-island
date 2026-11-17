using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Interop;
using DynamicIsland.Services;

namespace DynamicIsland.Island;

public partial class IslandWindow : Window
{
    private const double Flare = 10;

    private enum View { None, Compact, Expanded, Notification }

    private readonly IslandStateMachine _sm;
    private readonly Func<DateTimeOffset> _now;
    private readonly Spring _w = new(120), _h = new(0), _opacity = new(0);
    private readonly DispatcherTimer _pointerTimer;
    private IntPtr _hwnd;
    private bool _animating;
    private TimeSpan _lastRender;
    private bool _clickThrough;
    private bool _pointerInside;

    private View _shown = View.None;     // view currently in the content host
    private View _wanted = View.None;    // view the state machine asks for
    private object? _shownKey;           // identity of the shown content (e.g. the notification)

    public IslandWindow(IslandStateMachine sm, MediaService media, AudioLevelService audio, Func<DateTimeOffset> now)
    {
        InitializeComponent();
        _sm = sm;
        _now = now;
        ExpandedView.Media = media;
        CompactView.Eq.LevelSource = audio.TryGetLevels;
        _pointerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Input, (_, _) => PollPointer(), Dispatcher);
        _pointerTimer.Stop();

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE);
            ex |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
            SetClickThrough(true);
        };
        IslandRoot.MouseLeftButtonUp += OnIslandClick;
        Position();
        SystemEvents_DisplayChanged();
    }

    private void SystemEvents_DisplayChanged() =>
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(Position);

    private void Position()
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
    }

    /// <summary>Call after the state machine's Changed event.</summary>
    public void Render()
    {
        var (view, w, h) = _sm.Mode switch
        {
            IslandMode.MusicCompact => (View.Compact, 220.0, 34.0),
            IslandMode.MusicExpanded => (View.Expanded, 420.0, 110.0),
            IslandMode.Notification => (View.Notification, 400.0, 80.0),
            _ => (View.None, 120.0, 0.0),
        };
        _w.Target = w;
        _h.Target = h;

        if (_sm.IsFullscreen)
        {
            // Spec: hide instantly over a fullscreen app — no shrink animation over the game.
            _wanted = View.None;
            _w.Snap(); _h.Snap(); _opacity.Target = 0; _opacity.Snap();
            SwapContent();
            ApplyFrame();
            if (_animating) { CompositionTarget.Rendering -= OnRendering; _animating = false; }
            HideIsland();
            return;
        }

        if (view != View.None && !IsVisible)
        {
            Position();
            Show();
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            SetClickThrough(true);
            _pointerTimer.Start();
        }

        _wanted = view;
        var key = WantedKey();
        if (view == _shown && Equals(key, _shownKey))
        {
            UpdateShownContent();          // same view: refresh data in place, no fade
            _opacity.Target = view == View.None ? 0 : 1;
        }
        else
        {
            _opacity.Target = 0;            // fade out, swap in OnRendering when invisible
        }
        StartAnimating();
    }

    private object? WantedKey() => _wanted == View.Notification ? _sm.CurrentNotification : null;

    private void SwapContent()
    {
        _shown = _wanted;
        _shownKey = WantedKey();
        CompactView.Visibility = _shown == View.Compact ? Visibility.Visible : Visibility.Collapsed;
        ExpandedView.Visibility = _shown == View.Expanded ? Visibility.Visible : Visibility.Collapsed;
        NotifView.Visibility = _shown == View.Notification ? Visibility.Visible : Visibility.Collapsed;
        UpdateShownContent();
        _opacity.Target = _shown == View.None ? 0 : 1;
    }

    private void UpdateShownContent()
    {
        switch (_shown)
        {
            case View.Compact when _sm.Media is { } m: CompactView.Update(m); break;
            case View.Expanded when _sm.Media is { } m: ExpandedView.Update(m, _now); break;
            case View.Notification when _sm.CurrentNotification is { } n: NotifView.Update(n); break;
        }
    }

    // ---------------- animation ----------------

    private void StartAnimating()
    {
        if (_animating) return;
        _animating = true;
        _lastRender = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (t == _lastRender) return;               // Rendering can fire twice per frame
        var dt = _lastRender == TimeSpan.Zero ? 1.0 / 60 : Math.Min((t - _lastRender).TotalSeconds, 0.05);
        _lastRender = t;

        _w.Step(dt);
        _h.Step(dt);
        _opacity.Step(dt);

        if ((_shown != _wanted || !Equals(WantedKey(), _shownKey)) && _opacity.Value < 0.05)
            SwapContent();

        ApplyFrame();

        if (_w.IsSettled && _h.IsSettled && _opacity.IsSettled && _shown == _wanted)
        {
            CompositionTarget.Rendering -= OnRendering;
            _animating = false;
            if (_wanted == View.None) HideIsland();
        }
    }

    private void ApplyFrame()
    {
        var bw = Math.Max(0, _w.Value);
        var h = Math.Max(0, _h.Value);
        IslandRoot.Width = bw + 2 * Flare;
        IslandRoot.Height = Math.Max(0, h);
        if (h < 1)
        {
            NotchPath.Data = Geometry.Empty;
            ContentHost.Clip = Geometry.Empty;
        }
        else
        {
            var r = h <= 40 ? h / 2 : 22;
            var g = Geometry.Parse(NotchGeometry.BuildPathData(bw, h, Flare, r));
            g.Freeze();
            NotchPath.Data = g;
            ContentHost.Clip = new RectangleGeometry(new Rect(0, 0, bw, h), Math.Min(r, bw / 2), Math.Min(r, h / 2));
        }
        ContentHost.Width = bw;
        ContentHost.Height = h;
        var o = Math.Clamp(_opacity.Value, 0, 1);
        ContentHost.Opacity = o;
        ContentScale.ScaleX = ContentScale.ScaleY = 0.92 + 0.08 * o;
    }

    private void HideIsland()
    {
        _pointerTimer.Stop();
        if (_pointerInside)
        {
            _pointerInside = false;
            _sm.OnPointerLeave(_now());
        }
        Hide();
    }

    // ---------------- pointer / click-through ----------------

    private void PollPointer()
    {
        if (!IsVisible || _hwnd == IntPtr.Zero) return;
        if (!Native.GetCursorPos(out var p)) return;
        var local = IslandRoot.PointFromScreen(new Point(p.X, p.Y));
        var inside = local.X >= Flare && local.X <= Flare + _w.Value && local.Y >= 0 && local.Y <= Math.Max(_h.Value, 0)
                     && _wanted != View.None;

        SetClickThrough(!inside);
        if (inside == _pointerInside) return;
        _pointerInside = inside;
        if (inside) _sm.OnPointerEnter(_now());
        else _sm.OnPointerLeave(_now());
    }

    private void SetClickThrough(bool on)
    {
        if (_hwnd == IntPtr.Zero || on == _clickThrough) return;
        _clickThrough = on;
        var ex = (long)Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE);
        ex = on ? ex | Native.WS_EX_TRANSPARENT : ex & ~(long)Native.WS_EX_TRANSPARENT;
        Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
    }

    private void OnIslandClick(object sender, MouseButtonEventArgs e)
    {
        var n = _sm.OnClick(_now());
        if (n is not null) AppLauncher.TryActivate(n.AppUserModelId);
    }
}
