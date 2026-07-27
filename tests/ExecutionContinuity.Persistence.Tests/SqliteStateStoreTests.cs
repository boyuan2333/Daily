using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Xunit;

namespace ExecutionContinuity.Persistence.Tests;

public sealed class SqliteStateStoreTests
{
    [Fact]
    public async Task Restart_after_final_step_completion_keeps_other_routes_inactive()
    {
        var path = NewDatabasePath();
        try
        {
            var completedRoute = Route.Create("Completed", Step.Create("Action", "Done", "Boundary"));
            var otherRoute = Route.Create("Other", Step.Create("Other action", "Other done", "Other boundary"));
            var state = StateTransitions.SelectActiveRoute(
                AppState.Create(completedRoute, otherRoute),
                completedRoute.Id);
            state = StateTransitions.CompleteCurrentStep(state);

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            Assert.Null(recovered.Execution.ActiveRouteId);
            Assert.Equal(RouteLifecycle.Completed, recovered.Route(completedRoute.Id).Lifecycle);
            Assert.Equal(RouteLifecycle.Draft, recovered.Route(otherRoute.Id).Lifecycle);
            Assert.Null(recovered.Execution.CurrentStepId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Restart_after_route_switch_recovers_only_the_new_active_route()
    {
        var path = NewDatabasePath();
        try
        {
            var oldRoute = Route.Create("Old", Step.Create("Old action", "Old done", "Old boundary"));
            var newRoute = Route.Create("New", Step.Create("New action", "New done", "New boundary"));
            var state = StateTransitions.SelectActiveRoute(AppState.Create(oldRoute, newRoute), oldRoute.Id);
            state = StateTransitions.SelectActiveRoute(
                state,
                newRoute.Id,
                new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            Assert.Equal(newRoute.Id, recovered.Execution.ActiveRouteId);
            Assert.Equal(newRoute.Steps[0].Id, recovered.Execution.CurrentStepId);
            Assert.Equal(RouteLifecycle.Paused, recovered.Route(oldRoute.Id).Lifecycle);
            Assert.Equal(oldRoute.Id, Assert.Single(recovered.Snapshots).RouteId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Restart_recovery_uses_the_latest_valid_snapshot_of_the_active_route()
    {
        var path = NewDatabasePath();
        try
        {
            var first = Step.Create("First", "First done", "First boundary");
            var second = Step.Create("Second", "Second done", "Second boundary");
            var route = Route.Create("Route", first, second) with { Lifecycle = RouteLifecycle.Active };
            var snapshot = new ExecutionSnapshot(
                Guid.NewGuid(),
                route.Id,
                first.Id,
                first.Action,
                first.CompletionStandard,
                first.DoNotDo,
                first.FallbackAction,
                new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero),
                null);
            var state = AppState.Restore(
                new[] { route },
                new ExecutionState(route.Id, second.Id),
                new[] { snapshot },
                Array.Empty<CaptureEntry>());

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            Assert.Equal(first.Id, recovered.Execution.CurrentStepId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Saved_capture_and_pause_anchor_survive_store_recreation()
    {
        var path = NewDatabasePath();
        try
        {
            var step = Step.Create("Action", "Done", "Boundary", "Fallback");
            var route = Route.Create("Route", step);
            var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
            state = StateTransitions.Capture(
                state,
                "raw idea",
                new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));
            state = StateTransitions.Pause(
                state,
                new DateTimeOffset(2026, 7, 25, 12, 1, 0, TimeSpan.Zero));

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            Assert.Equal(state.Routes.Select(item => item.Id), recovered.Routes.Select(item => item.Id));
            Assert.Equal(state.Route(route.Id).Title, recovered.Route(route.Id).Title);
            Assert.Equal(
                state.Route(route.Id).Steps.Select(item => item with { }),
                recovered.Route(route.Id).Steps.Select(item => item with { }));
            Assert.Equal(state.Execution, recovered.Execution);
            Assert.Equal(state.Captures, recovered.Captures);
            Assert.Equal(state.Snapshots, recovered.Snapshots);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Failed_write_does_not_replace_the_previous_durable_state()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
            var initial = AppState.Create(route);
            await new SqliteStateStore(path).SaveAsync(initial);
            var changed = StateTransitions.Capture(
                initial,
                "must not commit",
                new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));

            var failingStore = new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => failingStore.SaveAsync(changed));

            var recovered = await new SqliteStateStore(path).LoadAsync();
            Assert.Equal(initial.Routes.Select(item => item.Id), recovered.Routes.Select(item => item.Id));
            Assert.Equal(initial.Route(route.Id).Title, recovered.Route(route.Id).Title);
            Assert.Empty(recovered.Captures);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"execution-continuity-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        if (File.Exists($"{path}-wal"))
        {
            File.Delete($"{path}-wal");
        }

        if (File.Exists($"{path}-shm"))
        {
            File.Delete($"{path}-shm");
        }
    }
}
