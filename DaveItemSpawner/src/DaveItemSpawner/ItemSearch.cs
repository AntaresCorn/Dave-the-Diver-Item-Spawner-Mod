namespace DaveItemSpawner;

public static class ItemSearch
{
    public static bool TryParseTid(string? query, out int tid)
    {
        query = query?.Trim();
        return int.TryParse(query, out tid) && tid > 0;
    }

    public static IReadOnlyList<ItemEntry> Filter(
        IEnumerable<ItemEntry> entries,
        string? query,
        int maxResults)
    {
        var safeMax = Math.Clamp(maxResults, 1, 500);
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return entries
                .OrderBy(e => e.Tid)
                .Take(safeMax)
                .ToArray();
        }

        if (TryParseTid(trimmed, out var tid))
        {
            return entries
                .Where(e => e.Tid == tid || e.Tid.ToString().Contains(trimmed, StringComparison.Ordinal))
                .OrderBy(e => e.Tid == tid ? 0 : 1)
                .ThenBy(e => e.Tid)
                .Take(safeMax)
                .ToArray();
        }

        return entries
            .Where(e =>
            {
                var label = e.Label ?? string.Empty;
                var textId = e.TextId ?? string.Empty;

                return label.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    textId.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    e.Route.ToString().Contains(trimmed, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(e => (e.Label ?? string.Empty).StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(e => e.Label ?? string.Empty)
            .Take(safeMax)
            .ToArray();
    }
}
