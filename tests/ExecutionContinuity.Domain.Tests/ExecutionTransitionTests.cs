using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class ExecutionTransitionTests
{
    [Fact]
    public void Capture_adds_only_an_inbox_entry_and_preserves_execution_state()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var capturedAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        var captured = StateTransitions.Capture(state, "raw interruption", capturedAt);

        var entry = Assert.Single(captured.Captures);
        Assert.Equal("raw interruption", entry.RawText);
        Assert.Equal(capturedAt, entry.CapturedAt);
        Assert.Equal(state.Execution, captured.Execution);
        Assert.Equal(state.Routes, captured.Routes);
        Assert.Empty(captured.Snapshots);
    }

    [Fact]
    public void Capture_after_a_pause_preserves_the_existing_snapshot()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var paused = StateTransitions.Pause(
            state,
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));

        var captured = StateTransitions.Capture(
            paused,
            "second thought",
            new DateTimeOffset(2026, 7, 25, 12, 1, 0, TimeSpan.Zero));

        Assert.Equal(paused.Snapshots, captured.Snapshots);
        Assert.Single(captured.Captures);
    }

    [Fact]
    public void Pause_without_a_note_persists_a_complete_return_anchor()
    {
        var step = Step.Create("Action", "Done", "Boundary", "Fallback");
        var route = Route.Create("Route", step);
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var pausedAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        var paused = StateTransitions.Pause(state, pausedAt);

        var snapshot = Assert.Single(paused.Snapshots);
        Assert.Equal(route.Id, snapshot.RouteId);
        Assert.Equal(step.Id, snapshot.StepId);
        Assert.Equal(step.Action, snapshot.CurrentAction);
        Assert.Equal(step.CompletionStandard, snapshot.CompletionStandard);
        Assert.Equal(step.DoNotDo, snapshot.DoNotDo);
        Assert.Equal(step.FallbackAction, snapshot.FallbackAction);
        Assert.Equal(pausedAt, snapshot.PausedAt);
        Assert.Null(snapshot.Note);
        Assert.Equal(route.Id, paused.Execution.ActiveRouteId);
        Assert.Equal(step.Id, paused.Execution.CurrentStepId);
        Assert.Equal(RouteLifecycle.Active, paused.Route(route.Id).Lifecycle);
    }

    [Fact]
    public void Completing_the_final_step_completes_the_route_and_clears_active_state()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);

        var completed = StateTransitions.CompleteCurrentStep(state);

        Assert.True(completed.Route(route.Id).Steps.Single().IsCompleted);
        Assert.Equal(RouteLifecycle.Completed, completed.Route(route.Id).Lifecycle);
        Assert.Null(completed.Execution.ActiveRouteId);
        Assert.Null(completed.Execution.CurrentStepId);
        completed.ValidateInvariants();
    }

    [Fact]
    public void Completing_a_fallback_returns_to_the_original_unfinished_step()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary", "Fallback"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);

        var fallback = StateTransitions.StartFallback(state);
        var returned = StateTransitions.CompleteFallback(fallback);

        Assert.Equal(ExecutionMode.Fallback, fallback.Execution.Mode);
        Assert.Equal(ExecutionMode.Normal, returned.Execution.Mode);
        Assert.Equal(state.Execution.CurrentStepId, returned.Execution.CurrentStepId);
        Assert.False(returned.Route(route.Id).Steps.Single().IsCompleted);
    }
}
