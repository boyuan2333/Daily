using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class HistoryEventTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Starting_a_route_records_a_start_event_without_a_snapshot()
    {
        var route = Route.Create("Prepare the session", Step.Create("Check input", "Done", "Boundary"));

        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, StartedAt);

        Assert.Empty(state.Snapshots);
        var recorded = Assert.Single(state.History);
        Assert.Equal(HistoryEventKind.RouteStarted, recorded.Kind);
        Assert.Equal(route.Id, recorded.RouteId);
        Assert.Equal(route.Steps[0].Id, recorded.StepId);
        Assert.Equal("Prepare the session", recorded.Summary);
        Assert.Equal(StartedAt, recorded.OccurredAt);
    }

    [Fact]
    public void Completing_a_step_records_the_step_as_a_fact()
    {
        var step = Step.Create("Check input", "Input checked", "Do not record yet");
        var route = Route.Create("Prepare the session", step);
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, StartedAt);

        var completed = StateTransitions.CompleteCurrentStep(state, StartedAt.AddMinutes(5));

        var recorded = Assert.Single(completed.History, item => item.Kind == HistoryEventKind.StepCompleted);
        Assert.Equal(step.Action, recorded.Summary);
        Assert.Equal(step.Id, recorded.StepId);
        Assert.Equal(StartedAt.AddMinutes(5), recorded.OccurredAt);
    }

    [Fact]
    public void Completing_the_final_step_records_the_step_then_the_route_completion()
    {
        var route = Route.Create("Prepare the session", Step.Create("Check input", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, StartedAt);

        var completed = StateTransitions.CompleteCurrentStep(state, StartedAt.AddMinutes(5));

        Assert.Equal(
            [HistoryEventKind.RouteStarted, HistoryEventKind.StepCompleted, HistoryEventKind.RouteCompleted],
            completed.History.Select(item => item.Kind));
        Assert.Equal("Prepare the session", completed.History[^1].Summary);
        Assert.Equal(route.Id, completed.History[^1].RouteId);
    }

    [Fact]
    public void Pausing_records_the_pause_without_rewriting_earlier_history()
    {
        var route = Route.Create("Prepare the session", Step.Create("Check input", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, StartedAt);

        var paused = StateTransitions.Pause(state, StartedAt.AddMinutes(1), "note");

        Assert.Equal(
            [HistoryEventKind.RouteStarted, HistoryEventKind.Paused],
            paused.History.Select(item => item.Kind));
        Assert.Equal(state.History[0], paused.History[0]);
    }

    [Fact]
    public void Switching_routes_records_the_pause_then_the_new_start()
    {
        var oldRoute = Route.Create("Old", Step.Create("Old action", "Done", "Boundary"));
        var newRoute = Route.Create("New", Step.Create("New action", "Done", "Boundary"));
        var started = StateTransitions.SelectActiveRoute(
            AppState.Create(oldRoute, newRoute),
            oldRoute.Id,
            StartedAt);

        var switched = StateTransitions.SelectActiveRoute(started, newRoute.Id, StartedAt.AddMinutes(10));

        Assert.Equal(
            [HistoryEventKind.RouteStarted, HistoryEventKind.Paused, HistoryEventKind.RouteStarted],
            switched.History.Select(item => item.Kind));
        Assert.Equal(oldRoute.Id, switched.History[^2].RouteId);
        Assert.Equal(newRoute.Id, switched.History[^1].RouteId);
    }

    [Fact]
    public void Returning_to_a_previously_switched_route_is_recorded_as_a_resume()
    {
        var oldRoute = Route.Create("Old", Step.Create("Old action", "Done", "Boundary"));
        var newRoute = Route.Create("New", Step.Create("New action", "Done", "Boundary"));
        var started = StateTransitions.SelectActiveRoute(
            AppState.Create(oldRoute, newRoute),
            oldRoute.Id,
            StartedAt);
        var switched = StateTransitions.SelectActiveRoute(started, newRoute.Id, StartedAt.AddMinutes(10));

        var resumed = StateTransitions.SelectActiveRoute(switched, oldRoute.Id, StartedAt.AddMinutes(20));

        Assert.Equal(HistoryEventKind.RouteResumed, resumed.History[^1].Kind);
        Assert.Equal(oldRoute.Id, resumed.History[^1].RouteId);
    }

    [Fact]
    public void Capture_never_records_history_or_touches_execution_state()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, StartedAt);
        var baseline = state.History;

        var captured = StateTransitions.Capture(state, "idea", StartedAt.AddMinutes(3));

        Assert.Equal(baseline, captured.History);
        Assert.Equal(state.Execution, captured.Execution);
        Assert.Empty(captured.Snapshots);
    }
}
