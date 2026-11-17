using System.Diagnostics;
using DynamicIsland.Interop;

namespace DynamicIsland.Services;

/// <summary>Opens / focuses the app that sent a notification, by its AppUserModelId.</summary>
public static class AppLauncher
{
    public static bool TryActivate(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return false;
        try
        {
            // Packaged (Store) apps.
            var mgr = (Native.IApplicationActivationManager)new Native.ApplicationActivationManager();
            if (mgr.ActivateApplication(aumid, null, 0, out _) >= 0) return true;
        }
        catch (Exception ex) { Log.Write($"AppLauncher COM failed for {aumid}: {ex.Message}"); }

        // Only hand to Explorer if the AUMID resolves; otherwise Explorer opens a random folder window.
        if (Native.SHParseDisplayName($"shell:AppsFolder\\{aumid}", IntPtr.Zero, out var pidl, 0, out _) != 0) return false;
        Native.CoTaskMemFree(pidl);

        try
        {
            // Win32 apps whose Start-menu shortcut carries this AUMID (Discord, Chrome, ...).
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{aumid}") { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) { Log.Error("AppLauncher.Explorer", ex); return false; }
    }
}
