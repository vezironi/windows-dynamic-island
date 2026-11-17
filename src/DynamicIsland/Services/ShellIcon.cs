using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DynamicIsland.Interop;

namespace DynamicIsland.Services;

/// <summary>Icon of an app from its Start-menu entry (shell:AppsFolder\AUMID), as PNG bytes. Call on an STA thread.</summary>
internal static class ShellIcon
{
    public static byte[]? TryGetPng(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return null;
        var pidl = IntPtr.Zero;
        var info = new Native.SHFILEINFO();
        try
        {
            if (Native.SHParseDisplayName($"shell:AppsFolder\\{aumid}", IntPtr.Zero, out pidl, 0, out _) != 0) return null;
            Native.SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf<Native.SHFILEINFO>(),
                Native.SHGFI_PIDL | Native.SHGFI_ICON | Native.SHGFI_LARGEICON);
            if (info.hIcon == IntPtr.Zero) return null;

            var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }
        catch (Exception ex) { Log.Error("ShellIcon", ex); return null; }
        finally
        {
            if (info.hIcon != IntPtr.Zero) Native.DestroyIcon(info.hIcon);
            if (pidl != IntPtr.Zero) Native.CoTaskMemFree(pidl);
        }
    }
}
