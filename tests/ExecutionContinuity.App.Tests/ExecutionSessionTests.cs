using ExecutionContinuity.App;
using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class ExecutionSessionTests
{
    [Fact]
    public void Guide_presentation_exposes_only_the_commands_allowed_by_the_execution_mode()
    {
        var fallbackRoute = Route.Create("Fallback", Step.Create("Action", "Done", "Boundary", "Fallback action"));
        var fallbackState = StateTransitions.StartFallback(
            StateTransitions.SelectActiveRoute(AppState.Create(fallbackRoute), fallbackRoute.Id));
        var fallback = GuidePresentation.From(fallbackState);

        Assert.Equal(GuideScreen.Fallback, fallback.Screen);
        Assert.Equal("Fallback action", fallback.Action);
        Assert.True(fallback.CanCompleteFallback);
        Assert.False(fallback.CanCompleteCurrentStep);
        Assert.False(fallback.CanStartFallback);

        var blockedRoute = Route.Create("Blocked", Step.Create("Action", "Done", "Boundary"));
        var blockedState = StateTransitions.RecordBlockAndPause(
            StateTransitions.SelectActiveRoute(AppState.Create(blockedRoute), blockedRoute.Id),
            "I cannot find the file",
            DateTimeOffset.Now);
        var blocked = GuidePresentation.From(blockedState);

        Assert.Equal(GuideScreen.Blocked, blocked.Screen);
        Assert.True(blocked.CanReturnFromBlocked);
        Assert.True(blocked.CanPause);
        Assert.False(blocked.CanCompleteCurrentStep);

        var idle = GuidePresentation.From(AppState.Create());
        Assert.Equal(GuideScreen.NoActiveRoute, idle.Screen);
        Assert.True(idle.CanCapture);
    }

    [Fact]
    public async Task Failed_capture_keeps_the_visible_and_durable_state_unchanged()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
            var initial = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
            await new SqliteStateStore(path).SaveAsync(initial);
            var session = new ExecutionSession(new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure")));
            await session.LoadAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync("interruption"));

            Assert.Empty(session.State.Captures);
            Assert.Equal(initial.Execution, session.State.Execution);
            Assert.Empty((await new ExecutionSession(new SqliteStateStore(path)).LoadAsync()).Captures);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Failed_final_completion_does_not_show_the_no_active_route_state()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
            var initial = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
            await new SqliteStateStore(path).SaveAsync(initial);
            var session = new ExecutionSession(new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure")));
            await session.LoadAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.CompleteCurrentStepAsync());

            Assert.Equal(route.Id, session.State.Execution.ActiveRouteId);
            Assert.False(session.State.Route(route.Id).Steps.Single().IsCompleted);
            var reloaded = await new ExecutionSession(new SqliteStateStore(path)).LoadAsync();
            Assert.Equal(route.Id, reloaded.Execution.ActiveRouteId);
            Assert.False(reloaded.Route(route.Id).Steps.Single().IsCompleted);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Failed_route_switch_keeps_the_original_active_route_after_restart()
    {
        var path = NewDatabasePath();
        try
        {
            var oldRoute = Route.Create("Old", Step.Create("Old action", "Done", "Boundary"));
            var newRoute = Route.Create("New", Step.Create("New action", "Done", "Boundary"));
            var initial = StateTransitions.SelectActiveRoute(AppState.Create(oldRoute, newRoute), oldRoute.Id);
            await new SqliteStateStore(path).SaveAsync(initial);
            var session = new ExecutionSession(new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure")));
            await session.LoadAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.ActivateRouteAsync(newRoute.Id));

            Assert.Equal(oldRoute.Id, session.State.Execution.ActiveRouteId);
            var reloaded = await new ExecutionSession(new SqliteStateStore(path)).LoadAsync();
            Assert.Equal(oldRoute.Id, reloaded.Execution.ActiveRouteId);
            Assert.Empty(reloaded.Snapshots);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Fallback_and_blocked_guide_states_survive_session_recreation()
    {
        var fallbackPath = NewDatabasePath();
        var blockedPath = NewDatabasePath();
        try
        {
            var fallbackRoute = Route.Create("Fallback", Step.Create("Action", "Done", "Boundary", "Fallback action"));
            var fallback = new ExecutionSession(new SqliteStateStore(fallbackPath));
            await fallback.LoadAsync();
            await fallback.AddRouteAsync(fallbackRoute);
            await fallback.ActivateRouteAsync(fallbackRoute.Id);
            await fallback.StartFallbackAsync();

            var recoveredFallback = await new ExecutionSession(new SqliteStateStore(fallbackPath)).LoadAsync();
            Assert.Equal(ExecutionMode.Fallback, recoveredFallback.Execution.Mode);
            Assert.Equal(fallbackRoute.Steps.Single().Id, recoveredFallback.Execution.CurrentStepId);

            var blockedRoute = Route.Create("Blocked", Step.Create("Action", "Done", "Boundary"));
            var blocked = new ExecutionSession(new SqliteStateStore(blockedPath));
            await blocked.LoadAsync();
            await blocked.AddRouteAsync(blockedRoute);
            await blocked.ActivateRouteAsync(blockedRoute.Id);
            await blocked.RecordBlockAndPauseAsync("I cannot find the file");

            var recoveredBlocked = new ExecutionSession(new SqliteStateStore(blockedPath));
            await recoveredBlocked.LoadAsync();
            Assert.Equal(ExecutionMode.Blocked, recoveredBlocked.State.Execution.Mode);
            await recoveredBlocked.ReturnFromBlockedAsync();
            Assert.Equal(ExecutionMode.Normal, recoveredBlocked.State.Execution.Mode);
        }
        finally
        {
            DeleteDatabase(fallbackPath);
            DeleteDatabase(blockedPath);
        }
    }

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"execution-continuity-app-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        foreach (var candidate in new[] { path, $"{path}-wal", $"{path}-shm" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
