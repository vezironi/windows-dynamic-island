using System.IO;
using System.Windows.Threading;
using DynamicIsland.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland.Services;

public enum NotificationAccess { Allowed, Denied, Error }

/// <summary>
/// Reads other apps' toasts via UserNotificationListener. The NotificationChanged event is not
/// available to unpackaged apps (spike: 0x80070490), so new toasts are found by polling every second.
/// </summary>
public sealed class NotificationService : IDisposable
{
    private readonly SeenNotifications _seen = new();
    private readonly Dictionary<string, byte[]?> _logoCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Dispatcher _ui = null!;

    public event Action<IslandNotification>? NotificationArrived;

    public static NotificationAccess CurrentAccess()
    {
        try
        {
            return UserNotificationListener.Current.GetAccessStatus() == UserNotificationListenerAccessStatus.Allowed
                ? NotificationAccess.Allowed : NotificationAccess.Denied;
        }
        catch (Exception ex) { Log.Error("Notifications.Access", ex); return NotificationAccess.Error; }
    }

    /// <summary>Shows Windows' consent prompt if needed. Call from the UI thread.</summary>
    public static async Task<NotificationAccess> RequestAccessAsync()
    {
        try
        {
            var s = await UserNotificationListener.Current.RequestAccessAsync();
            return s == UserNotificationListenerAccessStatus.Allowed ? NotificationAccess.Allowed : NotificationAccess.Denied;
        }
        catch (Exception ex) { Log.Error("Notifications.RequestAccess", ex); return NotificationAccess.Error; }
    }

    /// <summary>Starts polling if access is granted. Never throws. Safe to call again after access changes.</summary>
    public async Task<NotificationAccess> StartAsync(Dispatcher ui)
    {
        _ui = ui;
        var access = CurrentAccess();
        if (access != NotificationAccess.Allowed) return access;
        if (_cts is not null) return access; // already running

        try
        {
            var existing = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
            _seen.Seed(existing.Select(n => n.Id.ToString()));
        }
        catch (Exception ex) { Log.Error("Notifications.Seed", ex); return NotificationAccess.Error; }

        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);
        return access;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try { await PollOnceAsync(); }
                catch (Exception ex) { Log.Error("Notifications.Poll", ex); }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task PollOnceAsync()
    {
        var list = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
        foreach (var un in list.OrderBy(n => n.CreationTime))
        {
            var id = un.Id.ToString();
            if (!_seen.MarkIfNew(id)) continue;

            var binding = un.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding is null) continue;
            var texts = binding.GetTextElements().Select(t => t.Text ?? "").ToList();

            string appName = "", aumid = "";
            try
            {
                appName = un.AppInfo.DisplayInfo.DisplayName;
                aumid = un.AppInfo.AppUserModelId;
            }
            catch (Exception ex) { Log.Error("Notifications.AppInfo", ex); }

            var logo = await GetLogoAsync(un, aumid);
            var mapped = NotificationMapper.TryMap(id, appName, aumid, texts, logo, ownAumid: null);
            if (mapped is not null)
                await _ui.InvokeAsync(() => NotificationArrived?.Invoke(mapped));
        }
    }

    private async Task<byte[]?> GetLogoAsync(UserNotification un, string aumid)
    {
        if (_logoCache.TryGetValue(aumid, out var cached)) return cached;
        byte[]? bytes = null;
        try
        {
            // Packaged apps (WhatsApp, Teams, ...) expose a logo; Win32 apps return null here.
            var reference = un.AppInfo.DisplayInfo.GetLogo(new global::Windows.Foundation.Size(64, 64));
            if (reference is not null)
            {
                using var ras = await reference.OpenReadAsync();
                using var stream = ras.AsStreamForRead();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }
        }
        catch (Exception ex) { Log.Error("Notifications.Logo", ex); }
        bytes ??= await _ui.InvokeAsync(() => ShellIcon.TryGetPng(aumid));
        _logoCache[aumid] = bytes;
        return bytes;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts = null;
    }
}
