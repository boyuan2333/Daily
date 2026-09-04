using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public sealed record RouteListSection(string Title, IReadOnlyList<Route> Routes);

public sealed record RouteListItem(
    Route Route,
    string NextAction,
    string ProgressText,
    DateTimeOffset? PausedAt,
    string? CompletionStandard,
    string? DoNotDo,
    string? PauseNote,
    string? FallbackAction)
{
    public bool IsPaused => Route.Lifecycle == RouteLifecycle.Paused;
}

public static class RouteListPresentation
{
    public static IReadOnlyList<Route> Search(AppState state, string? query)
    {
        var normalized = query?.Trim();
        var routes = state.Routes.Where(route => route.Lifecycle != RouteLifecycle.Archived);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return routes.OrderBy(route => route.Title).ToArray();
        }

        return routes
            .Where(route =>
            {
                var item = Describe(state, route);
                return Contains(route.Title, normalized) || Contains(item.NextAction, normalized);
            })
            .OrderBy(route => route.Title)
            .ToArray();
    }

    public static IReadOnlyList<RouteListSection> GroupByStatus(AppState state)
    {
        var sections = new (string Title, RouteLifecycle Lifecycle)[]
        {
            ("Current", RouteLifecycle.Active),
            ("Paused", RouteLifecycle.Paused),
            ("Draft", RouteLifecycle.Draft),
            ("Completed", RouteLifecycle.Completed)
        };

        return sections
            .Select(section => new RouteListSection(
                section.Title,
                OrderRoutes(state, section.Lifecycle).ToArray()))
            .Where(section => section.Routes.Count > 0)
            .ToArray();
    }

    public static RouteListItem Describe(AppState state, Route route)
    {
        var step = route.CurrentStep();
        var snapshot = state.NewestValidSnapshotFor(route.Id);
        var actionStep = snapshot is not null
            ? route.Steps.SingleOrDefault(candidate => candidate.Id == snapshot.StepId) ?? step
            : step;

        return new RouteListItem(
            route,
            snapshot?.CurrentAction ?? actionStep?.Action ?? "没有未完成的动作",
            $"{route.Steps.Count(step => step.IsCompleted)}/{route.Steps.Count}",
            snapshot?.PausedAt,
            snapshot?.CompletionStandard ?? actionStep?.CompletionStandard,
            snapshot?.DoNotDo ?? actionStep?.DoNotDo,
            snapshot?.Note,
            snapshot?.FallbackAction ?? actionStep?.FallbackAction);
    }

    private static IEnumerable<Route> OrderRoutes(AppState state, RouteLifecycle lifecycle)
    {
        var routes = state.Routes.Where(route => route.Lifecycle == lifecycle);
        return lifecycle == RouteLifecycle.Paused
            ? routes.OrderByDescending(route => state.NewestValidSnapshotFor(route.Id)?.PausedAt ?? DateTimeOffset.MinValue)
                .ThenBy(route => route.Title)
            : routes.OrderBy(route => route.Title);
    }

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
