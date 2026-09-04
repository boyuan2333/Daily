using ExecutionContinuity.Domain;

namespace ExecutionContinuity.Persistence;

public sealed class FailSaveStateStore : IStateStore
{
    private readonly IStateStore _inner;

    public FailSaveStateStore(IStateStore inner)
    {
        _inner = inner;
    }

    public Task<AppState> LoadAsync(CancellationToken cancellationToken = default) =>
        _inner.LoadAsync(cancellationToken);

    public Task SaveAsync(AppState state, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Injected UI fixture write failure.");
}
