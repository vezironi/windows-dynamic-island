using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DynamicIsland.Core;

namespace DynamicIsland.Island;

public partial class MusicCompactView : UserControl
{
    public MusicCompactView() => InitializeComponent();

    public void Update(MediaInfo m)
    {
        TitleText.Text = m.Title;
        var img = Ui.ImageFromBytes(m.Thumbnail);
        Cover.Background = img is null ? null : new ImageBrush(img) { Stretch = Stretch.UniformToFill };
        CoverGlyph.Visibility = img is null ? Visibility.Visible : Visibility.Collapsed;
        Eq.SetPlaying(m.IsPlaying);
    }
}
