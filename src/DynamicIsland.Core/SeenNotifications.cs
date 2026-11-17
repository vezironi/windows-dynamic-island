namespace DynamicIsland.Core;

/// <summary>Remembers notification ids so each one is shown at most once (bounded, FIFO eviction).</summary>
public sealed class SeenNotifications
{
    private const int Capacity = 500;
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();

    /// <summary>Ids present at startup: they are never shown.</summary>
    public void Seed(IEnumerable<string> existingIds)
    {
        foreach (var id in existingIds) MarkIfNew(id);
    }

    /// <summary>True only the first time an id is seen.</summary>
    public bool MarkIfNew(string id)
    {
        if (!_set.Add(id)) return false;
        _order.Enqueue(id);
        if (_order.Count > Capacity) _set.Remove(_order.Dequeue());
        return true;
    }
}
