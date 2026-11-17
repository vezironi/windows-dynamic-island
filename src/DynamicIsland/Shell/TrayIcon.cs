using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DynamicIsland.Shell;

/// <summary>System tray icon with the Turkish menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _iconImage;

    public TrayIcon(bool startWithWindows, bool playSound,
        Action<bool> onStartWithWindows, Action<bool> onPlaySound, Action onGuide, Action onExit)
    {
        var startItem = new ToolStripMenuItem("Windows ile başlat") { CheckOnClick = true, Checked = startWithWindows };
        startItem.CheckedChanged += (_, _) => onStartWithWindows(startItem.Checked);
        var soundItem = new ToolStripMenuItem("Bildirim sesi") { CheckOnClick = true, Checked = playSound };
        soundItem.CheckedChanged += (_, _) => onPlaySound(soundItem.Checked);
        var guideItem = new ToolStripMenuItem("Kurulum rehberi");
        guideItem.Click += (_, _) => onGuide();
        var exitItem = new ToolStripMenuItem("Çıkış");
        exitItem.Click += (_, _) => onExit();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([startItem, soundItem, new ToolStripSeparator(), guideItem, exitItem]);

        _iconImage = CreateIcon();
        _icon = new NotifyIcon
        {
            Icon = _iconImage,
            Text = "Dynamic Island",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => onGuide();
    }

    /// <summary>A white pill on transparent background — readable on light and dark taskbars with an outline.</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var path = new GraphicsPath();
            const int x = 3, y = 10, w = 26, h = 12;
            path.AddArc(x, y, h, h, 90, 180);
            path.AddArc(x + w - h, y, h, h, 270, 180);
            path.CloseFigure();
            g.FillPath(Brushes.Black, path);
            using var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f);
            g.DrawPath(pen, path);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _iconImage.Dispose();
    }
}
