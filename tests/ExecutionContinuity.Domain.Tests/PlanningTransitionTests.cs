using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class PlanningTransitionTests
{
    [Fact]
    public void Planning_update_cannot_remove_the_step_referenced_by_the_newest_valid_snapshot()
    {
        var protectedStep = Step.Create("Protected", "Done", "Boundary");
        var route = Route.Create("Route", protectedStep, Step.Create("Later", "Done", "Boundary"));
        var active = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var paused = StateTransitions.Pause(active, DateTimeOffset.Now);

        Assert.Throws<InvalidOperationException>(() =>
            StateTransitions.UpdateRoute(
                paused,
                route.Id,
                "Changed title",
                new[] { paused.Route(route.Id).Steps.Single(step => step.Id != protectedStep.Id) }));
    }

    [Fact]
    public void Planning_update_preserves_ordered_steps_and_route_lifecycle()
    {
        var route = Route.Create("Route", Step.Create("First", "Done", "Boundary"));
        var second = Step.Create("Second", "Done", "Boundary");
        var state = AppState.Create(route);

        var updated = StateTransitions.UpdateRoute(state, route.Id, "Edited", new[] { second, route.Steps.Single() });

        Assert.Equal("Edited", updated.Route(route.Id).Title);
        Assert.Equal(new[] { "Second", "First" }, updated.Route(route.Id).Steps.Select(step => step.Action));
        Assert.Equal(new[] { 0, 1 }, updated.Route(route.Id).Steps.Select(step => step.Position));
        Assert.Equal(RouteLifecycle.Draft, updated.Route(route.Id).Lifecycle);
    }

    [Fact]
    public void Planning_can_archive_a_non_active_route_and_a_capture_without_deleting_them()
    {
        var route = Route.Create("Draft", Step.Create("Action", "Done", "Boundary"));
        var captured = StateTransitions.Capture(AppState.Create(route), "idea", DateTimeOffset.Now);

        var archived = StateTransitions.ArchiveCapture(
            StateTransitions.ArchiveRoute(captured, route.Id),
            captured.Captures.Single().Id);

        Assert.Equal(RouteLifecycle.Archived, archived.Route(route.Id).Lifecycle);
        Assert.True(archived.Captures.Single().IsArchived);
        Assert.Single(archived.Routes);
        Assert.Single(archived.Captures);
    }

    [Fact]
    public void Converting_a_capture_creates_a_draft_and_archives_only_the_source_capture()
    {
        var captured = StateTransitions.Capture(AppState.Create(), "raw idea", DateTimeOffset.Now);
        var route = Route.Create("Idea route", Step.Create("raw idea", "Done", "Boundary"));

        var converted = StateTransitions.ConvertCaptureToRoute(captured, captured.Captures.Single().Id, route);

        Assert.Equal(route, Assert.Single(converted.Routes));
        Assert.True(converted.Captures.Single().IsArchived);
        Assert.Null(converted.Execution.ActiveRouteId);
    }

    [Fact]
    public void Starting_a_fallback_requires_a_normal_execution_mode()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary", "Fallback"));
        var blocked = StateTransitions.RecordBlockAndPause(
            StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id),
            "I cannot find the file",
            DateTimeOffset.Now);

        Assert.Throws<InvalidOperationException>(() => StateTransitions.StartFallback(blocked));
    }
}
