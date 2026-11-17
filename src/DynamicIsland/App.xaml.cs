using System.IO;
using System.Windows;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Island;
using DynamicIsland.Services;
using DynamicIsland.Shell;

namespace DynamicIsland;

public partial class App : Application
{
    private static readonly string SettingsPath = Path.Combine(Log.Dir, "settings.json");

    private Mutex? _single;
    private AppSettings _settings = new();
    private IslandStateMachine _sm = null!;
    private IslandWindow _window = null!;
    private MediaService _media = null!;
    private FullscreenMonitor _fullscreen = null!;
    private NotificationService _notifications = null!;
    private NotificationSound _sound = null!;
    private AudioLevelService _audio = null!;
    private TrayIcon? _tray;
    private DispatcherTimer _tick = null!;
    private OnboardingWindow? _onboarding;

    private static DateTimeOffset Now() => DateTimeOffset.Now;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _single = new Mutex(initiallyOwned: true, "DynamicIsland.SingleInstance", out var isFirst);
        if (!isFirst) { Shutdown(); return; }

        DispatcherUnhandledException += (_, a) => { Log.Error("Dispatcher", a.Exception); a.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, a) => Log.Write("FATAL " + a.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, a) => { Log.Error("Task", a.Exception); a.SetObserved(); };

        _settings = AppSettings.Load(SettingsPath);
        StartupRegistration.Set(_settings.StartWithWindows);

        _sm = new IslandStateMachine(IslandTimings.Default);
        _media = new MediaService();
        _fullscreen = new FullscreenMonitor();
        _notifications = new NotificationService();
        _sound = new NotificationSound { Enabled = _settings.PlaySound };
        _audio = new AudioLevelService();
        _window = new IslandWindow(_sm, _media, _audio, Now);

        _tick = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, (_, _) =>
        {
            _sm.Tick(Now());
            if (_sm.Mode == IslandMode.Hidden) _tick.Stop();
        }, Dispatcher);
        _tick.Stop();

        _sm.Changed += _window.Render;
        _sm.Changed += () =>
        {
            // Listen to the speakers only while the compact island (with the equalizer) shows playing music.
            if (_sm.Mode == IslandMode.MusicCompact && _sm.Media is { IsPlaying: true }) _audio.Start();
            else _audio.Stop();
        };
        _media.MediaChanged += m => { _sm.OnMediaChanged(m, Now()); EnsureTicking(); };
        _fullscreen.FullscreenChanged += f => { _sm.OnFullscreenChanged(f, Now()); EnsureTicking(); };
        _fullscreen.TopAreaOccupiedChanged += o => { _sm.OnTopAreaOccupiedChanged(o, Now()); EnsureTicking(); };
        _notifications.NotificationArrived += n =>
        {
            if (!_fullscreen.IsFullscreen) _sound.Play();
            _sm.OnNotification(n, Now());
            EnsureTicking();
        };

        _tray = new TrayIcon(_settings.StartWithWindows, _settings.PlaySound,
            onStartWithWindows: on => { UpdateSettings(_settings with { StartWithWindows = on }); StartupRegistration.Set(on); },
            onPlaySound: on => { UpdateSettings(_settings with { PlaySound = on }); _sound.Enabled = on; },
            onGuide: ShowOnboarding,
            onExit: ExitApp);

        _fullscreen.Start();
        await _media.StartAsync(Dispatcher);
        var access = await _notifications.StartAsync(Dispatcher);
        Log.Write($"Started. Notification access: {access}");

        if (!_settings.OnboardingDone || access != NotificationAccess.Allowed) ShowOnboarding();
    }

    private void EnsureTicking()
    {
        if (_sm.Mode != IslandMode.Hidden && !_tick.IsEnabled) _tick.Start();
    }

    private void UpdateSettings(AppSettings s)
    {
        _settings = s;
        try { s.Save(SettingsPath); }
        catch (Exception ex) { Log.Error("Settings.Save", ex); }
    }

    private void ShowOnboarding()
    {
        if (_onboarding is not null) { _onboarding.Activate(); return; }
        _onboarding = new OnboardingWindow(async () => await _notifications.StartAsync(Dispatcher));
        _onboarding.Closed += (_, _) =>
        {
            _onboarding = null;
            _ = _notifications.StartAsync(Dispatcher);   // user may have granted access in Settings
            if (!_settings.OnboardingDone) UpdateSettings(_settings with { OnboardingDone = true });
        };
        _onboarding.Show();
        _onboarding.Activate();
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _tray = null;
        _notifications.Dispose();
        _media.Dispose();
        _fullscreen.Dispose();
        _audio.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
