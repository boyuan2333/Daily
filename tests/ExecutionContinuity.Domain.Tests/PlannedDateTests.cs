using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class PlannedDateTests
{
    [Fact]
    public void Setting_a_planned_date_never_changes_execution_state_steps_or_snapshots()
    {
        var step = Step.Create("Action", "Done", "Boundary");
        var route = Route.Create("Route", step);
        var active = StateTransitions.SelectActiveRoute(
            AppState.Create(route),
            route.Id,
            new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero));
        var paused = StateTransitions.Pause(active, new DateTimeOffset(2026, 9, 21, 8, 5, 0, TimeSpan.Zero));

        var scheduled = StateTransitions.SetStepDate(
            paused,
            route.Id,
            step.Id,
            new DateOnly(2026, 9, 22),
            new TimeOnly(9, 30));

        Assert.Equal(paused.Execution, scheduled.Execution);
        Assert.Equal(paused.Snapshots, scheduled.Snapshots);
        Assert.Equal(paused.History, scheduled.History);
        var updated = scheduled.Route(route.Id).Steps.Single();
        Assert.Equal(new DateOnly(2026, 9, 22), updated.PlannedDate);
        Assert.Equal(new TimeOnly(9, 30), updated.PlannedTime);
        Assert.Equal(step.Action, updated.Action);
        Assert.Equal(step.IsCompleted, updated.IsCompleted);
    }

    [Fact]
    public void A_planned_time_requires_a_planned_date()
    {
        var step = Step.Create("Action", "Done", "Boundary");
        var route = Route.Create("Route", step);
        var state = AppState.Create(route);

        Assert.Throws<ArgumentException>(() =>
            StateTransitions.SetStepDate(state, route.Id, step.Id, null, new TimeOnly(9, 0)));
    }

    [Fact]
    public void A_step_without_a_date_stays_unscheduled()
    {
        var step = Step.Create("Action", "Done", "Boundary");
        var route = Route.Create("Route", step);

        Assert.Null(StateTransitions.AddRoute(AppState.Create(), route).Route(route.Id).Steps.Single().PlannedDate);
    }

    [Fact]
    public void A_step_can_be_created_with_a_planned_date_and_time()
    {
        var step = Step.Create(
            "Action",
            "Done",
            "Boundary",
            fallbackAction: null,
            plannedDate: new DateOnly(2026, 9, 22));

        Assert.Equal(new DateOnly(2026, 9, 22), step.PlannedDate);
        Assert.Null(step.PlannedTime);
    }
}
