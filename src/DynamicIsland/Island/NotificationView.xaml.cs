using System.Windows;
using System.Windows.Controls;
using DynamicIsland.Core;

namespace DynamicIsland.Island;

public partial class NotificationView : UserControl
{
    public NotificationView() => InitializeComponent();

    public void Update(IslandNotification n)
    {
        TitleText.Text = n.Title;
        AppText.Text = n.AppName;
        BodyText.Text = n.Body;
        BodyText.Visibility = string.IsNullOrEmpty(n.Body) ? Visibility.Collapsed : Visibility.Visible;
        var img = Ui.ImageFromBytes(n.AppLogo, 64);
        Logo.Source = img;
        LogoGlyph.Visibility = img is null ? Visibility.Visible : Visibility.Collapsed;
    }
}
