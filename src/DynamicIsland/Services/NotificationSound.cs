using System.IO;
using System.Media;

namespace DynamicIsland.Services;

/// <summary>The island's own short notification sound (Windows' own is silent under DND / banners off).</summary>
public sealed class NotificationSound
{
    private static readonly string WavPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Windows Notify Messaging.wav");
    private readonly SoundPlayer? _player;

    public NotificationSound()
    {
        try
        {
            if (File.Exists(WavPath))
            {
                _player = new SoundPlayer(WavPath);
                _player.Load();
            }
        }
        catch (Exception ex) { Log.Error("NotificationSound.Load", ex); }
    }

    public bool Enabled { get; set; } = true;

    public void Play()
    {
        if (!Enabled) return;
        try
        {
            if (_player is not null) _player.Play();
            else SystemSounds.Asterisk.Play();
        }
        catch (Exception ex) { Log.Error("NotificationSound.Play", ex); }
    }
}
