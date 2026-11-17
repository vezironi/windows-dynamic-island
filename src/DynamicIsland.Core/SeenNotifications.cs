namespace DynamicIsland.Core;

/// <summary>
/// Remembers notification ids so each one is shown at most once. Ids are forgotten only when they
/// leave the notification center (<see cref="Retain"/>) — never while still present, so a large
/// backlog can't be replayed.
/// </summary>
public sealed class SeenNotifications
{
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);

    /// <summary>Ids present at startup: they are never shown.</summary>
    public void Seed(IEnumerable<string> existingIds) => _set.UnionWith(existingIds);

    /// <summary>Forget ids that are no longer in the notification center.</summary>
    public void Retain(IEnumerable<string> presentIds) => _set.IntersectWith(presentIds);

    /// <summary>True only the first time an id is seen.</summary>
    public bool MarkIfNew(string id) => _set.Add(id);
}
