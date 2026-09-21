using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public sealed record ReviewTimelineEntry(
    HistoryEventKind Kind,
    Guid RouteId,
    Guid? StepId,
    string Summary,
    DateTimeOffset OccurredAt);

public static class ReviewPresentation
{
    public static IReadOnlyList<ReviewTimelineEntry> Timeline(AppState state) =>
        state.History
            .OrderByDescending(item => item.OccurredAt)
            .Select(item => new ReviewTimelineEntry(
                item.Kind,
                item.RouteId,
                item.StepId,
                item.Summary,
                item.OccurredAt))
            .ToArray();
}
