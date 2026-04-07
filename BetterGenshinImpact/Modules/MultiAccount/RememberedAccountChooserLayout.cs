namespace BetterGenshinImpact.Modules.MultiAccount;

public sealed record RememberedAccountEntry(string Text, int Left, int Top, int Width, int Height)
{
    public int Bottom => Top + Height;
}

public static class RememberedAccountChooserLayout
{
    private const int ExpandedListGap = 8;

    public static bool IsMaskedAccountText(string? text)
    {
        var normalized = RememberedAccountMatcher.Normalize(text);
        return normalized.Contains('*', StringComparison.Ordinal);
    }

    public static bool IsExpanded(IEnumerable<RememberedAccountEntry> entries)
    {
        return entries.Count(static entry => IsMaskedAccountText(entry.Text)) >= 2;
    }

    public static RememberedAccountEntry? TryGetChooserAnchor(IEnumerable<RememberedAccountEntry> entries)
    {
        return entries
            .Where(static entry => IsMaskedAccountText(entry.Text))
            .OrderBy(static entry => entry.Top)
            .ThenBy(static entry => entry.Left)
            .FirstOrDefault();
    }

    public static RememberedAccountEntry? TryPickTargetEntry(IEnumerable<RememberedAccountEntry> entries, string targetAccountLabel)
    {
        var normalizedTarget = RememberedAccountMatcher.Normalize(targetAccountLabel);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return null;
        }

        var maskedEntries = entries
            .Where(static entry => IsMaskedAccountText(entry.Text))
            .OrderBy(static entry => entry.Top)
            .ThenBy(static entry => entry.Left)
            .ToList();

        if (maskedEntries.Count == 0)
        {
            return null;
        }

        var matchingEntries = maskedEntries
            .Where(entry => RememberedAccountMatcher.Matches(normalizedTarget, entry.Text))
            .ToList();

        if (matchingEntries.Count == 0)
        {
            return null;
        }

        var anchor = TryGetChooserAnchor(maskedEntries);
        if (anchor != null)
        {
            var expandedMatch = matchingEntries.FirstOrDefault(entry => entry.Top > anchor.Bottom + ExpandedListGap);
            if (expandedMatch != null)
            {
                return expandedMatch;
            }
        }

        return matchingEntries[^1];
    }

    public static bool IsCurrentAccountMatch(IEnumerable<RememberedAccountEntry> entries, string targetAccountLabel)
    {
        var anchor = TryGetChooserAnchor(entries);
        return anchor != null && RememberedAccountMatcher.Matches(targetAccountLabel, anchor.Text);
    }
}
