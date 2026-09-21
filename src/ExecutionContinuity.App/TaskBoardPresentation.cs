using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public enum TasksView
{
    Calendar,
    List
}

public sealed record TaskProjection(
    Guid RouteId,
    string RouteTitle,
    RouteLifecycle RouteLifecycle,
    Guid StepId,
    string Action,
    int Position,
    bool IsReady,
    DateOnly? PlannedDate,
    TimeOnly? PlannedTime);

public sealed record TaskBucket(DateOnly? Date, IReadOnlyList<TaskProjection> Tasks);

public static class TaskBoardPresentation
{
    public static IReadOnlyList<TaskProjection> Tasks(AppState state, string? query = null) =>
        Filter(
            state.Routes
                .Where(route => route.Lifecycle != RouteLifecycle.Archived)
                .OrderBy(route => route.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(route => route.Id)
                .SelectMany(route => route.Steps
                    .Where(step => !step.IsCompleted)
                    .OrderBy(step => step.Position)
                    .Select(step => new TaskProjection(
                        route.Id,
                        route.Title,
                        route.Lifecycle,
                        step.Id,
                        step.Action,
                        step.Position,
                        !string.IsNullOrWhiteSpace(step.CompletionStandard),
                        step.PlannedDate,
                        step.PlannedTime)))
                .ToArray(),
            query);

    public static IReadOnlyList<TaskBucket> CalendarBuckets(AppState state, string? query = null)
    {
        var tasks = Tasks(state, query);
        var buckets = tasks
            .Where(task => task.PlannedDate is not null)
            .GroupBy(task => task.PlannedDate!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new TaskBucket(
                group.Key,
                group
                    .OrderBy(task => task.PlannedTime ?? TimeOnly.MinValue)
                    .ThenBy(task => task.RouteTitle, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Position)
                    .ToArray()))
            .ToList();

        var unscheduled = tasks.Where(task => task.PlannedDate is null).ToArray();
        if (unscheduled.Length > 0)
        {
            buckets.Add(new TaskBucket(null, unscheduled));
        }

        return buckets;
    }

    public static IReadOnlyList<TaskProjection> Filter(IReadOnlyList<TaskProjection> tasks, string? query)
    {
        var normalized = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return tasks;
        }

        return tasks
            .Where(task =>
                task.Action.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                task.RouteTitle.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
