using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class RouteSwitchingTests
{
    [Fact]
    public void Planning_can_add_a_draft_route_without_activating_it()
    {
        var route = Route.Create("Prepared", Step.Create("Action", "Done", "Boundary"));

        var planned = StateTransitions.AddRoute(AppState.Create(), route);

        Assert.Equal(route, Assert.Single(planned.Routes));
        Assert.Equal(RouteLifecycle.Draft, planned.Route(route.Id).Lifecycle);
        Assert.Null(planned.Execution.ActiveRouteId);
        Assert.Null(planned.Execution.CurrentStepId);
    }

    [Fact]
    public void Default_route_selection_cannot_switch_away_from_an_active_route_without_an_anchor_time()
    {
        var oldRoute = Route.Create("Old", Step.Create("Old action", "Old done", "Old boundary"));
        var newRoute = Route.Create("New", Step.Create("New action", "New done", "New boundary"));
        var started = StateTransitions.SelectActiveRoute(AppState.Create(oldRoute, newRoute), oldRoute.Id);

        Assert.Throws<InvalidOperationException>(() =>
            StateTransitions.SelectActiveRoute(started, newRoute.Id));
    }

    [Fact]
    public void Capturing_without_an_active_route_does_not_create_or_activate_a_route()
    {
        var route = Route.Create("Draft", Step.Create("Action", "Done", "Boundary"));
        var state = AppState.Create(route);

        var captured = StateTransitions.Capture(
            state,
            "idea while idle",
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));

        Assert.Null(captured.Execution.ActiveRouteId);
        Assert.Null(captured.Execution.CurrentStepId);
        Assert.Equal(RouteLifecycle.Draft, captured.Route(route.Id).Lifecycle);
        Assert.Single(captured.Captures);
    }

    [Fact]
    public void Switching_routes_persists_the_old_route_anchor_before_new_route_activation()
    {
        var oldStep = Step.Create("Old action", "Old done", "Old boundary", "Old fallback");
        var oldRoute = Route.Create("Old", oldStep);
        var newRoute = Route.Create("New", Step.Create("New action", "New done", "New boundary"));
        var started = StateTransitions.SelectActiveRoute(AppState.Create(oldRoute, newRoute), oldRoute.Id);
        var switchedAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        var switched = StateTransitions.SelectActiveRoute(started, newRoute.Id, switchedAt);

        var snapshot = Assert.Single(switched.Snapshots);
        Assert.Equal(oldRoute.Id, snapshot.RouteId);
        Assert.Equal(oldStep.Id, snapshot.StepId);
        Assert.Equal(RouteLifecycle.Paused, switched.Route(oldRoute.Id).Lifecycle);
        Assert.Equal(RouteLifecycle.Active, switched.Route(newRoute.Id).Lifecycle);
        Assert.Equal(newRoute.Steps[0].Id, switched.Execution.CurrentStepId);
    }

    [Fact]
    public void A_route_without_an_unfinished_step_cannot_become_active()
    {
        var completed = Route.Create("Completed", Step.Create("Action", "Done", "Boundary")) with
        {
            Lifecycle = RouteLifecycle.Completed,
            Steps = new[] { Step.Create("Action", "Done", "Boundary") with { IsCompleted = true } }
        };

        Assert.Throws<InvalidOperationException>(() =>
            StateTransitions.SelectActiveRoute(AppState.Create(completed), completed.Id));
    }
}
