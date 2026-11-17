using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynamicIsland.Services;

namespace DynamicIsland.Island;

internal static class Ui
{
    private static byte[]? _lastBytes;
    private static ImageSource? _lastImage;

    /// <summary>Decodes image bytes (cover art, app logo). Caches the last result since covers repeat.</summary>
    public static ImageSource? ImageFromBytes(byte[]? bytes, int decodeWidth = 128)
    {
        if (bytes is null || bytes.Length == 0) return null;
        if (ReferenceEquals(bytes, _lastBytes)) return _lastImage;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.DecodePixelWidth = decodeWidth;
            img.StreamSource = new MemoryStream(bytes);
            img.EndInit();
            img.Freeze();
            (_lastBytes, _lastImage) = (bytes, img);
            return img;
        }
        catch (Exception ex) { Log.Error("Ui.ImageFromBytes", ex); return null; }
    }

    public static string FormatTime(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}
