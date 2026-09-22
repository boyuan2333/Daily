using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;

namespace ExecutionContinuity.App;

public sealed class ExecutionSession
{
    private readonly IStateStore _store;
    private readonly SemaphoreSlim _commandGate = new(1, 1);

    public ExecutionSession(IStateStore store)
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

    public Task OrganizeCaptureAsync(
        Guid captureId,
        string? organizedText,
        CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.OrganizeCapture(state, captureId, organizedText), cancellationToken);

    public Task SetLanguagePreferenceAsync(
        LanguagePreference preference,
        CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.SetLanguagePreference(state, preference), cancellationToken);

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
        CommitAsync(state => StateTransitions.CompleteCurrentStep(state, DateTimeOffset.Now), cancellationToken);

    public Task AddRouteAsync(
        Route route,
        string? projectName = null,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => StateTransitions.SetRouteProject(
                StateTransitions.AddRoute(state, route),
                route.Id,
                projectName),
            cancellationToken);

    public Task UpdateRouteAsync(
        Guid routeId,
        string title,
        IReadOnlyList<Step> steps,
        string? projectName = null,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => StateTransitions.SetRouteProject(
                StateTransitions.UpdateRoute(state, routeId, title, steps),
                routeId,
                projectName),
            cancellationToken);

    public Task SetRouteProjectAsync(
        Guid routeId,
        string? projectName,
        CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.SetRouteProject(state, routeId, projectName), cancellationToken);

    public Task AddTaskFlowAsync(TaskFlow flow, CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.AddTaskFlow(state, flow), cancellationToken);

    public Task CreateTaskFlowFromRouteAsync(
        Guid routeId,
        string title,
        CancellationToken cancellationToken = default) =>
        CommitAsync(state => StateTransitions.CreateTaskFlowFromRoute(state, routeId, title), cancellationToken);

    public Task StartTaskFlowAsync(
        Guid taskFlowId,
        string? note = null,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => StateTransitions.StartTaskFlow(state, taskFlowId, DateTimeOffset.Now, note),
            cancellationToken);

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
        string? projectName = null,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => StateTransitions.SetRouteProject(
                StateTransitions.ConvertCaptureToRoute(state, captureId, route),
                route.Id,
                projectName),
            cancellationToken);

    public Task ActivateRouteAsync(Guid routeId, string? note = null, CancellationToken cancellationToken = default) =>
        CommitAsync(
            state => state.Execution.ActiveRouteId == routeId
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
