using DynamicIsland.Services;
using Microsoft.Win32;

namespace DynamicIsland.Shell;

/// <summary>"Windows ile başlat" via HKCU\...\Run.</summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DynamicIsland";

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled && Environment.ProcessPath is { } exe) key.SetValue(ValueName, $"\"{exe}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) { Log.Error("StartupRegistration", ex); }
    }
}
