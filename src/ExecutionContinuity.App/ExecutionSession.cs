using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;

namespace ExecutionContinuity.App;

public sealed class ExecutionSession
{
    private readonly SqliteStateStore _store;
    private readonly SemaphoreSlim _commandGate = new(1, 1);

    public ExecutionSession(SqliteStateStore store)
    {
        _store = store;
    }

    public AppState State { get; private set; } = AppState.Create();

    public async Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _commandGate.WaitAsync(cancellationToken);
        try
        {
            State = await _store.LoadAsync(cancellationToken);
            return State;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    public Task CaptureAsync(string rawText, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.Capture(state, rawText, DateTimeOffset.Now), cancellationToken);

    public Task PauseAsync(string? note = null, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.Pause(state, DateTimeOffset.Now, note), cancellationToken);

    public Task RecordBlockAndPauseAsync(string blockDescription, CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => StateTransitions.RecordBlockAndPause(state, blockDescription, DateTimeOffset.Now),
            cancellationToken);

    public Task ReturnFromBlockedAsync(CancellationToken cancellationToken = default) =>
        CommitAsync(StateTransitions.ReturnFromBlocked, cancellationToken);

    public Task StartFallbackAsync(CancellationToken cancellationToken = default) =>
        CommitAsync(StateTransitions.StartFallback, cancellationToken);

    public Task CompleteFallbackAsync(CancellationToken cancellationToken = default) =>
        CommitAsync(StateTransitions.CompleteFallback, cancellationToken);

    public Task CompleteCurrentStepAsync(CancellationToken cancellationToken = default) =>
        CommitAsync(StateTransitions.CompleteCurrentStep, cancellationToken);

    public Task AddRouteAsync(Route route, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.AddRoute(state, route), cancellationToken);

    public Task UpdateRouteAsync(
        Guid routeId,
        string title,
        IReadOnlyList<Step> steps,
        CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.UpdateRoute(state, routeId, title, steps), cancellationToken);

    public Task ArchiveRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.ArchiveRoute(state, routeId), cancellationToken);

    public Task RestoreArchivedRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.RestoreArchivedRoute(state, routeId), cancellationToken);

    public Task ArchiveCaptureAsync(Guid captureId, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.ArchiveCapture(state, captureId), cancellationToken);

    public Task RestoreArchivedCaptureAsync(Guid captureId, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.RestoreArchivedCapture(state, captureId), cancellationToken);

    public Task ConvertCaptureToRouteAsync(
        Guid captureId,
        Route route,
        CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.ConvertCaptureToRoute(state, captureId, route), cancellationToken);

    public Task ActivateRouteAsync(Guid routeId, string? note = null, CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => state.Execution.ActiveRouteId is null || state.Execution.ActiveRouteId == routeId
                ? StateTransitions.SelectActiveRoute(state, routeId)
                : StateTransitions.SelectActiveRoute(state, routeId, DateTimeOffset.Now, note),
            cancellationToken);

    private async Task CommitAsync(
        Func<AppState, AppState> transition,
        CancellationToken cancellationToken)
    {
        await _commandGate.WaitAsync(cancellationToken);
        try
        {
            var candidate = transition(State);
            await _store.SaveAsync(candidate, cancellationToken);
            State = candidate;
        }
        finally
        {
            _commandGate.Release();
        }
    }
}
