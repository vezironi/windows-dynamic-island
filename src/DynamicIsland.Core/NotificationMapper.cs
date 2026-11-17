namespace DynamicIsland.Core;

public static class NotificationMapper
{
    public const string FallbackAppName = "Bildirim";

    /// <summary>
    /// Turns raw toast data into an island notification. Returns null for our own
    /// notifications and for toasts without any visible text.
    /// </summary>
    public static IslandNotification? TryMap(string id, string appName, string? aumid,
        IReadOnlyList<string> texts, byte[]? logo, string? ownAumid)
    {
        if (ownAumid is not null && string.Equals(aumid, ownAumid, StringComparison.OrdinalIgnoreCase))
            return null;

        var lines = texts.Select(t => t?.Trim() ?? "").Where(t => t.Length > 0).ToList();
        if (lines.Count == 0) return null;

        var name = string.IsNullOrWhiteSpace(appName) ? FallbackAppName : appName.Trim();
        return new IslandNotification(id, name, aumid, lines[0], string.Join("\n", lines.Skip(1)), logo);
    }
}
