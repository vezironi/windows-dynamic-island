using System.Diagnostics;
using System.Windows;
using DynamicIsland.Services;

namespace DynamicIsland.Shell;

public partial class OnboardingWindow : Window
{
    // Text per spike result (spec §10). If DND turns out to hide toasts from the listener,
    // switch to BannerOffText (spec §5.3 fallback).
    internal const string DndText =
        "Windows'ta \"Rahatsız Etme\" modunu açın. Böylece sağ alttaki Windows kutucukları çıkmaz; " +
        "bildirimler bildirim merkezine düşer ve ada onları gösterir. Ses de adadan gelir.";
    internal const string BannerOffText =
        "Bildirim ayarlarında, adada görmek istediğiniz her uygulama için (WhatsApp, Discord vb.) " +
        "\"Bildirim başlıklarını göster\" seçeneğini kapatın. Bildirimler merkeze düşer ve ada onları gösterir.";

    private readonly Func<Task> _onAccessGranted;

    public OnboardingWindow(Func<Task> onAccessGranted)
    {
        InitializeComponent();
        _onAccessGranted = onAccessGranted;
        SilenceText.Text = DndText;
        RefreshAccess(NotificationService.CurrentAccess());

        AllowButton.Click += async (_, _) =>
        {
            var access = await NotificationService.RequestAccessAsync();
            if (access != NotificationAccess.Allowed) Open("ms-settings:privacy-notifications");
            RefreshAccess(access);
            if (access == NotificationAccess.Allowed) await _onAccessGranted();
        };
        SettingsButton.Click += (_, _) => Open("ms-settings:notifications");
        DoneButton.Click += (_, _) => Close();
    }

    private void RefreshAccess(NotificationAccess access)
    {
        var ok = access == NotificationAccess.Allowed;
        AccessText.Text = ok
            ? "✓ İzin verildi. Ada diğer uygulamaların bildirimlerini okuyabiliyor."
            : "Adanın bildirimleri gösterebilmesi için Windows'tan izin gerekiyor. İzin verilmezse sadece müzik gösterilir.";
        AllowButton.IsEnabled = !ok;
    }

    private static void Open(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("Onboarding.Open", ex); }
    }
}
