using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public enum RoutesSectionView
{
    Routes,
    TaskFlows
}

public sealed record TaskFlowSummary(
    TaskFlow Flow,
    int StepCount,
    bool HasUnfinishedInstance,
    Guid? InstanceRouteId);

public static class TaskFlowPresentation
{
    public static IReadOnlyList<TaskFlowSummary> Flows(AppState state) =>
        state.TaskFlows
            .OrderBy(flow => flow.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(flow =>
            {
                var instance = state.Routes.FirstOrDefault(route =>
                    route.SourceTaskFlowId == flow.Id &&
                    route.Lifecycle is RouteLifecycle.Draft or RouteLifecycle.Active or RouteLifecycle.Paused);
                return new TaskFlowSummary(flow, flow.Steps.Count, instance is not null, instance?.Id);
            })
            .ToArray();
}
