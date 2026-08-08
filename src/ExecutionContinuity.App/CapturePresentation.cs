using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public enum PlanningDestination
{
    Routes,
    Inbox,
    Archive
}

public sealed record CaptureContext(
    bool PlanningMode,
    bool SettingsOpen,
    PlanningDestination PlanningDestination,
    bool EnteringBlock,
    Guid? ActiveRouteId,
    Guid? CurrentStepId,
    ExecutionMode ExecutionMode,
    string? CurrentAction,
    double MainScrollOffset,
    double RouteEditorScrollOffset);

public sealed class CaptureContextLock
{
    public CaptureContext? Origin { get; private set; }

    public bool IsOpen => Origin is not null;

    public bool CanChangeUnderlyingContext => !IsOpen;

    public void Open(CaptureContext origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (IsOpen)
        {
            throw new InvalidOperationException("Capture is already open.");
        }

        Origin = origin;
    }

    public CaptureContext CompleteSave() => Release();

    public CaptureContext Cancel() => Release();

    private CaptureContext Release()
    {
        var origin = Origin ?? throw new InvalidOperationException("Capture is not open.");
        Origin = null;
        return origin;
    }
}
