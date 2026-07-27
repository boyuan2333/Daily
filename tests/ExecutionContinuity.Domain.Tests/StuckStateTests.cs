using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class StuckStateTests
{
    [Fact]
    public void A_step_without_a_fallback_accepts_one_block_sentence_and_saves_the_anchor()
    {
        var step = Step.Create("Action", "Done", "Boundary");
        var route = Route.Create("Route", step);
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var pausedAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        var blocked = StateTransitions.RecordBlockAndPause(state, "I cannot find the file", pausedAt);

        var snapshot = Assert.Single(blocked.Snapshots);
        Assert.Equal("I cannot find the file", snapshot.Note);
        Assert.Equal(state.Execution.ActiveRouteId, blocked.Execution.ActiveRouteId);
        Assert.Equal(step.Id, blocked.Execution.CurrentStepId);
        Assert.Equal(ExecutionMode.Blocked, blocked.Execution.Mode);
    }

    [Fact]
    public void Returning_from_a_blocked_state_restores_the_same_normal_action()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var started = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var blocked = StateTransitions.RecordBlockAndPause(
            started,
            "I cannot find the file",
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));

        var returned = StateTransitions.ReturnFromBlocked(blocked);

        Assert.Equal(ExecutionMode.Normal, returned.Execution.Mode);
        Assert.Equal(started.Execution.CurrentStepId, returned.Execution.CurrentStepId);
        Assert.False(returned.Route(route.Id).Steps.Single().IsCompleted);
    }

    [Fact]
    public void A_block_description_cannot_contain_multiple_sentences_or_lines()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var timestamp = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            StateTransitions.RecordBlockAndPause(state, "First. Second.", timestamp));
        Assert.Throws<ArgumentException>(() =>
            StateTransitions.RecordBlockAndPause(state, "First line\nSecond line", timestamp));
    }
}
