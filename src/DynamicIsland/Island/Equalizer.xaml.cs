using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DynamicIsland.Island;

/// <summary>Four bouncing bars, like macOS / iOS now-playing. Static (low) while paused.</summary>
public partial class Equalizer : UserControl
{
    private static readonly double[] Durations = [0.42, 0.55, 0.37, 0.61];
    private bool _playing;

    public Equalizer() => InitializeComponent();

    public void SetPlaying(bool playing)
    {
        if (playing == _playing) return;
        _playing = playing;
        var bars = new[] { B1, B2, B3, B4 };
        for (var i = 0; i < bars.Length; i++)
        {
            var scale = (ScaleTransform)bars[i].RenderTransform;
            if (playing)
            {
                var a = new DoubleAnimation(0.25, 1.0, TimeSpan.FromSeconds(Durations[i]))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
            else
            {
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleY = 0.3;
            }
        }
    }
}
