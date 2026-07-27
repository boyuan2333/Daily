using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class RecoveryInvariantTests
{
    [Fact]
    public void Restoring_an_active_route_without_an_active_route_id_is_rejected()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary")) with
        {
            Lifecycle = RouteLifecycle.Active
        };

        Assert.Throws<InvalidOperationException>(() =>
            AppState.Restore(
                new[] { route },
                new ExecutionState(null, null),
                Array.Empty<ExecutionSnapshot>(),
                Array.Empty<CaptureEntry>()));
    }

    [Fact]
    public void An_invalid_historical_snapshot_is_retained_but_not_used_for_recovery()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary")) with
        {
            Lifecycle = RouteLifecycle.Active
        };
        var invalidSnapshot = new ExecutionSnapshot(
            Guid.NewGuid(),
            route.Id,
            Guid.NewGuid(),
            "Historical action",
            "Historical done",
            "Historical boundary",
            null,
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero),
            null);
        var state = AppState.Restore(
            new[] { route },
            new ExecutionState(route.Id, route.Steps[0].Id),
            new[] { invalidSnapshot },
            Array.Empty<CaptureEntry>());

        Assert.Single(state.Snapshots);
        Assert.Null(state.NewestValidSnapshotFor(route.Id));
    }

    [Fact]
    public void Restoring_a_completed_current_step_recalculates_to_the_next_unfinished_step()
    {
        var first = Step.Create("First", "First done", "First boundary");
        var second = Step.Create("Second", "Second done", "Second boundary");
        var draft = Route.Create("Route", first, second);
        var active = draft with
        {
            Lifecycle = RouteLifecycle.Active,
            Steps = draft.Steps.Select(step => step.Id == first.Id
                ? step with { IsCompleted = true }
                : step).ToArray()
        };

        var restored = AppState.Restore(
            new[] { active },
            new ExecutionState(active.Id, first.Id),
            Array.Empty<ExecutionSnapshot>(),
            Array.Empty<CaptureEntry>());

        Assert.Equal(second.Id, restored.Execution.CurrentStepId);
        restored.ValidateInvariants();
    }
}
