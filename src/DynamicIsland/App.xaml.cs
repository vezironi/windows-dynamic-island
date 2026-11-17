using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland;

// SPIKE (Task 1) — throwaway. Logs identity, notification access/contents and media info.
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynamicIsland", "spike.log");
    private TextBox _box = null!;
    private readonly HashSet<uint> _seen = new();

    private void Log(string s)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {s}";
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.AppendAllText(LogPath, line + Environment.NewLine);
        Dispatcher.BeginInvoke(() => { _box.AppendText(line + "\n"); _box.ScrollToEnd(); });
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _box = new TextBox { IsReadOnly = true, FontFamily = new("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap };
        var w = new Window { Title = "DynamicIsland spike", Width = 700, Height = 450, Content = _box };
        w.Closed += (_, _) => Shutdown();
        w.Show();

        try { Log("Identity: " + global::Windows.ApplicationModel.Package.Current.Id.FullName); }
        catch (Exception ex) { Log("Identity: NONE (" + ex.GetType().Name + ")"); }

        try
        {
            var listener = UserNotificationListener.Current;
            Log("AccessStatus(before): " + listener.GetAccessStatus());
            var status = await listener.RequestAccessAsync();
            Log("RequestAccessAsync: " + status);
            if (status == UserNotificationListenerAccessStatus.Allowed)
            {
                var initial = await listener.GetNotificationsAsync(NotificationKinds.Toast);
                foreach (var n in initial) _seen.Add(n.Id);
                Log($"Existing toasts at startup: {initial.Count}");
                try
                {
                    listener.NotificationChanged += (_, a) => Log($"EVENT NotificationChanged kind={a.ChangeKind} id={a.UserNotificationId}");
                    Log("NotificationChanged subscribed OK");
                }
                catch (Exception ex) { Log("NotificationChanged subscribe FAILED: " + ex.Message); }

                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                t.Tick += async (_, _) =>
                {
                    try
                    {
                        foreach (var n in await listener.GetNotificationsAsync(NotificationKinds.Toast))
                        {
                            if (!_seen.Add(n.Id)) continue;
                            var texts = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements().Select(x => x.Text) ?? [];
                            Log($"POLL new toast id={n.Id} app='{n.AppInfo.DisplayInfo.DisplayName}' aumid='{n.AppInfo.AppUserModelId}' texts=[{string.Join(" | ", texts)}]");
                        }
                    }
                    catch (Exception ex) { Log("poll error: " + ex.Message); }
                };
                t.Start();
            }
        }
        catch (Exception ex) { Log("Listener FAILED: " + ex); }

        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            async void Dump(string why)
            {
                try
                {
                    var s = mgr.GetCurrentSession();
                    if (s is null) { Log($"MEDIA[{why}] no session"); return; }
                    var p = await s.TryGetMediaPropertiesAsync();
                    var pb = s.GetPlaybackInfo();
                    Log($"MEDIA[{why}] src={s.SourceAppUserModelId} '{p.Title}' - '{p.Artist}' status={pb.PlaybackStatus} thumb={(p.Thumbnail != null)}");
                }
                catch (Exception ex) { Log("media error: " + ex.Message); }
            }
            mgr.CurrentSessionChanged += (_, _) => { Dump("session"); Hook(); };
            void Hook()
            {
                var s = mgr.GetCurrentSession();
                if (s is null) return;
                s.MediaPropertiesChanged += (_, _) => Dump("props");
                s.PlaybackInfoChanged += (_, _) => Dump("playback");
            }
            Hook();
            Dump("start");
        }
        catch (Exception ex) { Log("GSMTC FAILED: " + ex); }
    }
}
