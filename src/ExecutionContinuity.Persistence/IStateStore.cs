using ExecutionContinuity.Domain;

namespace ExecutionContinuity.Persistence;

public interface IStateStore
{
    Task<AppState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppState state, CancellationToken cancellationToken = default);
}
