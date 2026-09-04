using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public enum InboxFilter
{
    All,
    Unorganized,
    Organized
}

public static class InboxPresentation
{
    public static IReadOnlyList<CaptureEntry> Filter(
        IEnumerable<CaptureEntry> captures,
        InboxFilter filter,
        string? query)
    {
        var normalized = query?.Trim();
        return captures
            .Where(capture => !capture.IsArchived)
            .Where(capture => filter switch
            {
                InboxFilter.Unorganized => string.IsNullOrWhiteSpace(capture.OrganizedText),
                InboxFilter.Organized => !string.IsNullOrWhiteSpace(capture.OrganizedText),
                _ => true
            })
            .Where(capture => string.IsNullOrWhiteSpace(normalized) ||
                Contains(capture.RawText, normalized) ||
                Contains(capture.OrganizedText, normalized))
            .OrderByDescending(capture => capture.CapturedAt)
            .ToArray();
    }

    public static string DisplayText(CaptureEntry capture) =>
        string.IsNullOrWhiteSpace(capture.OrganizedText) ? capture.RawText : capture.OrganizedText;

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
}
