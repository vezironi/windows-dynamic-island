namespace DynamicIsland.Core;

public enum IslandMode { Hidden, MusicTucked, MusicCompact, MusicExpanded, Notification }

public sealed record MediaInfo(
    string Title,
    string Artist,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionUpdatedAt,
    byte[]? Thumbnail);

public sealed record IslandNotification(
    string Id,
    string AppName,
    string? AppUserModelId,
    string Title,
    string Body,
    byte[]? AppLogo);

public sealed record IslandTimings(
    TimeSpan HoverDelay,
    TimeSpan CollapseDelay,
    TimeSpan PausedHide,
    TimeSpan NotificationDuration,
    TimeSpan PeekDuration)
{
    public static IslandTimings Default { get; } = new(
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(4));
}
