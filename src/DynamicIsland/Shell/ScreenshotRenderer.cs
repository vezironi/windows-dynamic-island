using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shape = System.Windows.Shapes;
using DynamicIsland.Core;
using DynamicIsland.Island;

namespace DynamicIsland.Shell;

/// <summary>
/// `DynamicIsland.exe --screenshots &lt;dir&gt;` renders the real island views with made-up data
/// (fictional song, cover and message) onto a clean background — README images without personal data.
/// </summary>
internal static class ScreenshotRenderer
{
    private const double Flare = 10;
    private const int W = 900, H = 150;

    public static void RenderAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var now = DateTimeOffset.Now;
        var media = new MediaInfo("Midnight City Lights", "Neon Avenue", true,
            TimeSpan.FromSeconds(83), TimeSpan.FromSeconds(214), now, Cover());
        var note = new IslandNotification("1", "Chat", null, "Alex",
            "Are you coming tonight? Let's meet around 8 and figure out the place.", ChatLogo());

        var compact = new MusicCompactView();
        compact.Update(media);
        compact.Eq.ShowStaticLevels([0.85, 0.45, 0.7, 0.3]);
        Save(Scene(compact, 220, 34, browser: false), Path.Combine(dir, "compact.png"));

        var expanded = new MusicExpandedView();
        Save(Scene(expanded, 420, 110, browser: false, after: () => expanded.Update(media, () => now)),
            Path.Combine(dir, "expanded.png"));

        var notif = new NotificationView();
        notif.Update(note);
        Save(Scene(notif, 400, 80, browser: false), Path.Combine(dir, "notification.png"));

        Save(Scene(null, 80, 5, browser: true), Path.Combine(dir, "tucked.png"));
    }

    private static FrameworkElement Scene(FrameworkElement? content, double bw, double h, bool browser, Action? after = null)
    {
        var root = new Grid
        {
            Width = W, Height = H,
            Background = new LinearGradientBrush(
                [new GradientStop(Color.FromRgb(0x1B, 0x2A, 0x4A), 0), new GradientStop(Color.FromRgb(0x4B, 0x2C, 0x5E), 0.55),
                 new GradientStop(Color.FromRgb(0xC0, 0x5A, 0x6B), 1)], new Point(0, 0), new Point(1, 1)),
        };
        if (browser) root.Children.Add(FakeBrowser());

        var f = Math.Clamp(h / 2, 0, Flare);
        var r = h <= 40 ? h / 2 : 22;
        var island = new Grid { Width = bw + 2 * f, Height = h, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        island.Children.Add(new Shape.Path { Fill = Brushes.Black, Data = Geometry.Parse(NotchGeometry.BuildPathData(bw, h, f, r)) });
        if (content is not null)
        {
            content.Margin = new Thickness(f, 0, f, 0);
            content.Width = bw;
            content.Height = h;
            island.Children.Add(content);
        }
        root.Children.Add(island);

        Layout(root);
        if (after is not null) { after(); Layout(root); }
        return root;
    }

    private static UIElement FakeBrowser()
    {
        var bar = new Grid { Height = 44, VerticalAlignment = VerticalAlignment.Top, Background = new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24)) };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 0, 0) };
        foreach (var (title, active) in new[] { ("Inbox", false), ("Project docs", false), ("Music — Library", true), ("Weather", false) })
        {
            tabs.Children.Add(new Border
            {
                Width = 200, Height = 36, Margin = new Thickness(0, 0, 4, 0), CornerRadius = new CornerRadius(8, 8, 0, 0),
                Background = new SolidColorBrush(active ? Color.FromRgb(0x35, 0x36, 0x3A) : Color.FromRgb(0x28, 0x29, 0x2C)),
                Child = new TextBlock
                {
                    Text = title, Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED)), FontSize = 13,
                    FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0),
                },
            });
        }
        bar.Children.Add(tabs);
        var page = new Border { Margin = new Thickness(0, 44, 0, 0), Background = new SolidColorBrush(Color.FromRgb(0x35, 0x36, 0x3A)) };
        var g = new Grid();
        g.Children.Add(page);
        g.Children.Add(bar);
        return g;
    }

    private static void Layout(FrameworkElement e)
    {
        e.Measure(new Size(W, H));
        e.Arrange(new Rect(0, 0, W, H));
        e.UpdateLayout();
    }

    private static void Save(FrameworkElement e, string path)
    {
        var bmp = new RenderTargetBitmap(W * 2, H * 2, 192, 192, PixelFormats.Pbgra32);
        bmp.Render(e);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    // ---- made-up artwork ----

    private static byte[] Cover() => Draw(256, dc =>
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0xFF, 0x7A, 0x59), Color.FromRgb(0x7B, 0x2F, 0xF7), 45), null, new Rect(0, 0, 256, 256));
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), null, new Point(170, 90), 70, 70);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x40, 0x10, 0x10, 0x30)), null, new Point(80, 190), 90, 90);
    });

    private static byte[] ChatLogo() => Draw(128, dc =>
    {
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0x34, 0xC7, 0x59), Color.FromRgb(0x1F, 0x9E, 0x45), 90), null, new Rect(0, 0, 128, 128), 28, 28);
        var text = new FormattedText("", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe Fluent Icons"), 64, Brushes.White, 1.0);
        dc.DrawText(text, new Point((128 - text.Width) / 2, (128 - text.Height) / 2));
    });

    private static byte[] Draw(int size, Action<DrawingContext> draw)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) draw(dc);
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
