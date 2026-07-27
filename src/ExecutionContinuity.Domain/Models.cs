namespace ExecutionContinuity.Domain;

public enum RouteLifecycle
{
    Draft,
    Active,
    Paused,
    Completed,
    Archived
}

public enum ExecutionMode
{
    Normal,
    Fallback,
    Blocked
}

public sealed record Step(
    Guid Id,
    int Position,
    string Action,
    string CompletionStandard,
    string DoNotDo,
    string? FallbackAction,
    bool IsCompleted)
{
    public static Step Create(
        string action,
        string completionStandard,
        string doNotDo,
        string? fallbackAction = null) =>
        new(Guid.NewGuid(), 0, action, completionStandard, doNotDo, fallbackAction, false);
}

public sealed record Route(
    Guid Id,
    string Title,
    IReadOnlyList<Step> Steps,
    RouteLifecycle Lifecycle)
{
    public static Route Create(string title, params Step[] steps) =>
        new(
            Guid.NewGuid(),
            title,
            steps.Select((step, index) => step with { Position = index }).ToArray(),
            RouteLifecycle.Draft);

    public Step? CurrentStep() => Steps
        .Where(step => !step.IsCompleted)
        .OrderBy(step => step.Position)
        .FirstOrDefault();
}

public sealed record ExecutionSnapshot(
    Guid Id,
    Guid RouteId,
    Guid StepId,
    string CurrentAction,
    string CompletionStandard,
    string DoNotDo,
    string? FallbackAction,
    DateTimeOffset PausedAt,
    string? Note);

public sealed record CaptureEntry(Guid Id, string RawText, DateTimeOffset CapturedAt, bool IsArchived = false);

public sealed record ExecutionState(
    Guid? ActiveRouteId,
    Guid? CurrentStepId,
    ExecutionMode Mode = ExecutionMode.Normal);

public sealed class AppState
{
    private readonly IReadOnlyList<Route> _routes;
    private readonly IReadOnlyList<ExecutionSnapshot> _snapshots;
    private readonly IReadOnlyList<CaptureEntry> _captures;

    private AppState(
        IReadOnlyList<Route> routes,
        ExecutionState execution,
        IReadOnlyList<ExecutionSnapshot> snapshots,
        IReadOnlyList<CaptureEntry> captures)
    {
        _routes = routes;
        Execution = execution;
        _snapshots = snapshots;
        _captures = captures;
    }

    public IReadOnlyList<Route> Routes => _routes;

    public ExecutionState Execution { get; }

    public IReadOnlyList<ExecutionSnapshot> Snapshots => _snapshots;

    public IReadOnlyList<CaptureEntry> Captures => _captures;

    public static AppState Create(params Route[] routes)
    {
        var state = new AppState(
            routes.ToArray(),
            new ExecutionState(null, null),
            Array.Empty<ExecutionSnapshot>(),
            Array.Empty<CaptureEntry>());
        state.ValidateInvariants();
        return state;
    }

    public static AppState Restore(
        IEnumerable<Route> routes,
        ExecutionState execution,
        IEnumerable<ExecutionSnapshot> snapshots,
        IEnumerable<CaptureEntry> captures)
    {
        var routeArray = routes.ToArray();
        if (execution.ActiveRouteId is Guid activeRouteId)
        {
            var activeRoute = routeArray.Single(route => route.Id == activeRouteId);
            if (execution.CurrentStepId is Guid currentStepId &&
                activeRoute.Steps.All(step => step.Id != currentStepId))
            {
                throw new InvalidDataException("The persisted current step does not belong to the active route.");
            }

            var currentStep = activeRoute.Steps.FirstOrDefault(step => step.Id == execution.CurrentStepId);
            if (currentStep is null || currentStep.IsCompleted)
            {
                var recalculated = activeRoute.CurrentStep()
                    ?? throw new InvalidDataException("The active route has no unfinished step.");
                execution = execution with { CurrentStepId = recalculated.Id };
            }
        }

        var state = new AppState(
            routeArray,
            execution,
            snapshots.ToArray(),
            captures.ToArray());
        state.ValidateInvariants();
        return state;
    }

    public Route Route(Guid routeId) => _routes.Single(route => route.Id == routeId);

    public ExecutionSnapshot? NewestValidSnapshotFor(Guid routeId)
    {
        return Snapshots
            .Where(snapshot => snapshot.RouteId == routeId)
            .Where(snapshot => _routes.Any(route =>
                route.Id == snapshot.RouteId &&
                route.Steps.Any(step => step.Id == snapshot.StepId && !step.IsCompleted)))
            .OrderByDescending(snapshot => snapshot.PausedAt)
            .FirstOrDefault();
    }

    public AppState Recover()
    {
        if (Execution.ActiveRouteId is not Guid routeId)
        {
            return this;
        }

        var route = Route(routeId);
        var stepId = NewestValidSnapshotFor(routeId)?.StepId ?? route.CurrentStep()?.Id;
        if (stepId is null)
        {
            throw new InvalidDataException("The active route has no recoverable unfinished step.");
        }

        return With(Routes, Execution with { CurrentStepId = stepId.Value });
    }

    internal AppState With(
        IReadOnlyList<Route> routes,
        ExecutionState execution,
        IReadOnlyList<ExecutionSnapshot>? snapshots = null,
        IReadOnlyList<CaptureEntry>? captures = null)
    {
        var next = new AppState(
            routes,
            execution,
            snapshots ?? Snapshots,
            captures ?? Captures);
        next.ValidateInvariants();
        return next;
    }

    public void ValidateInvariants()
    {
        var activeRoutes = _routes.Where(route => route.Lifecycle == RouteLifecycle.Active).ToArray();

        if (Execution.ActiveRouteId is null)
        {
            if (activeRoutes.Length != 0 || Execution.CurrentStepId is not null)
            {
                throw new InvalidOperationException("No active route requires no active lifecycle and no current step.");
            }

            return;
        }

        if (activeRoutes.Length != 1 || activeRoutes[0].Id != Execution.ActiveRouteId)
        {
            throw new InvalidOperationException("activeRouteId must identify the only active route.");
        }

        var activeRoute = Route(Execution.ActiveRouteId.Value);
        if (Execution.CurrentStepId is not null &&
            activeRoute.Steps.All(step => step.Id != Execution.CurrentStepId))
        {
            throw new InvalidOperationException("Current step must belong to the active route.");
        }
    }
}

public static class StateTransitions
{
    public static AppState AddRoute(AppState state, Route route)
    {
        if (state.Routes.Any(existing => existing.Id == route.Id))
        {
            throw new InvalidOperationException("A route with the same ID already exists.");
        }

        if (route.Lifecycle != RouteLifecycle.Draft || route.Steps.Count == 0)
        {
            throw new ArgumentException("A new route must be a draft with at least one step.", nameof(route));
        }

        return state.With(state.Routes.Append(route).ToArray(), state.Execution);
    }

    public static AppState UpdateRoute(
        AppState state,
        Guid routeId,
        string title,
        IReadOnlyList<Step> steps)
    {
        if (string.IsNullOrWhiteSpace(title) || steps.Count == 0 ||
            steps.Select(step => step.Id).Distinct().Count() != steps.Count)
        {
            throw new ArgumentException("A route update requires a title and distinct ordered steps.");
        }

        var route = state.Route(routeId);
        var protectedSnapshot = state.NewestValidSnapshotFor(routeId);
        if (protectedSnapshot is not null && steps.All(step => step.Id != protectedSnapshot.StepId))
        {
            throw new InvalidOperationException("The newest return anchor's step must be retained or explicitly resolved.");
        }

        if (state.Execution.ActiveRouteId == routeId &&
            state.Execution.CurrentStepId is Guid currentStepId &&
            steps.All(step => step.Id != currentStepId))
        {
            throw new InvalidOperationException("The active current step must be retained.");
        }

        var updated = route with
        {
            Title = title.Trim(),
            Steps = steps.Select((step, index) => step with { Position = index }).ToArray()
        };
        return state.With(
            state.Routes.Select(item => item.Id == routeId ? updated : item).ToArray(),
            state.Execution);
    }

    public static AppState ArchiveRoute(AppState state, Guid routeId)
    {
        if (state.Execution.ActiveRouteId == routeId)
        {
            throw new InvalidOperationException("The active route cannot be archived.");
        }

        return state.With(
            state.Routes.Select(route => route.Id == routeId
                ? route with { Lifecycle = RouteLifecycle.Archived }
                : route).ToArray(),
            state.Execution);
    }

    public static AppState ArchiveCapture(AppState state, Guid captureId)
    {
        if (state.Captures.All(capture => capture.Id != captureId))
        {
            throw new InvalidOperationException("The capture does not exist.");
        }

        return state.With(
            state.Routes,
            state.Execution,
            captures: state.Captures.Select(capture => capture.Id == captureId
                ? capture with { IsArchived = true }
                : capture).ToArray());
    }

    public static AppState ConvertCaptureToRoute(AppState state, Guid captureId, Route route)
    {
        var capture = state.Captures.SingleOrDefault(item => item.Id == captureId);
        if (capture is null || capture.IsArchived)
        {
            throw new InvalidOperationException("Only an unarchived capture can be converted.");
        }

        var withRoute = AddRoute(state, route);
        return ArchiveCapture(withRoute, captureId);
    }

    public static AppState SelectActiveRoute(AppState state, Guid routeId)
    {
        if (state.Execution.ActiveRouteId is not null && state.Execution.ActiveRouteId != routeId)
        {
            throw new InvalidOperationException("Switching an active route requires a durable pause anchor.");
        }

        return SelectActiveRoute(state, routeId, null);
    }

    public static AppState SelectActiveRoute(
        AppState state,
        Guid routeId,
        DateTimeOffset? switchedAt,
        string? note = null)
    {
        var selected = state.Route(routeId);
        if (selected.Lifecycle is RouteLifecycle.Completed or RouteLifecycle.Archived ||
            selected.CurrentStep() is null)
        {
            throw new InvalidOperationException("Only a route with an unfinished step can become active.");
        }

        var snapshots = state.Snapshots;
        if (switchedAt is not null &&
            state.Execution.ActiveRouteId is Guid previousRouteId &&
            state.Execution.CurrentStepId is Guid previousStepId)
        {
            var previousRoute = state.Route(previousRouteId);
            var previousStep = previousRoute.Steps.Single(step => step.Id == previousStepId);
            snapshots = snapshots.Append(new ExecutionSnapshot(
                Guid.NewGuid(),
                previousRoute.Id,
                previousStep.Id,
                previousStep.Action,
                previousStep.CompletionStandard,
                previousStep.DoNotDo,
                previousStep.FallbackAction,
                switchedAt.Value,
                note)).ToArray();
        }
        var routes = state.Routes
            .Select(route => route with
            {
                Lifecycle = route.Id == routeId
                    ? RouteLifecycle.Active
                    : route.Lifecycle == RouteLifecycle.Active
                        ? RouteLifecycle.Paused
                        : route.Lifecycle
            })
            .ToArray();

        var currentStepId = selected.CurrentStep()?.Id;
        return state.With(
            routes,
            new ExecutionState(routeId, currentStepId),
            snapshots);
    }

    public static AppState Capture(AppState state, string rawText, DateTimeOffset capturedAt)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            throw new ArgumentException("Capture text cannot be empty.", nameof(rawText));
        }

        var captures = state.Captures
            .Append(new CaptureEntry(Guid.NewGuid(), rawText, capturedAt))
            .ToArray();
        return state.With(state.Routes, state.Execution, captures: captures);
    }

    public static AppState Pause(AppState state, DateTimeOffset pausedAt, string? note = null)
    {
        var routeId = state.Execution.ActiveRouteId
            ?? throw new InvalidOperationException("Cannot pause without an active route.");
        var stepId = state.Execution.CurrentStepId
            ?? throw new InvalidOperationException("Cannot pause without a current step.");
        var route = state.Route(routeId);
        var step = route.Steps.Single(step => step.Id == stepId);
        var snapshot = new ExecutionSnapshot(
            Guid.NewGuid(),
            route.Id,
            step.Id,
            step.Action,
            step.CompletionStandard,
            step.DoNotDo,
            step.FallbackAction,
            pausedAt,
            note);
        return state.With(
            state.Routes,
            state.Execution,
            state.Snapshots.Append(snapshot).ToArray());
    }

    public static AppState RecordBlockAndPause(
        AppState state,
        string blockDescription,
        DateTimeOffset pausedAt)
    {
        if (string.IsNullOrWhiteSpace(blockDescription) ||
            blockDescription.Contains('\n') ||
            blockDescription.Contains('\r') ||
            blockDescription.Count(character => character is '.' or '!' or '?') > 1)
        {
            throw new ArgumentException("The block description must be one sentence.", nameof(blockDescription));
        }

        var paused = Pause(state, pausedAt, blockDescription);
        return paused.With(
            paused.Routes,
            paused.Execution with { Mode = ExecutionMode.Blocked });
    }

    public static AppState ReturnFromBlocked(AppState state)
    {
        if (state.Execution.Mode != ExecutionMode.Blocked)
        {
            throw new InvalidOperationException("No blocked action is currently active.");
        }

        return state.With(
            state.Routes,
            state.Execution with { Mode = ExecutionMode.Normal });
    }

    public static AppState CompleteCurrentStep(AppState state)
    {
        if (state.Execution.Mode != ExecutionMode.Normal)
        {
            throw new InvalidOperationException("Only a normal current action can be completed.");
        }

        var routeId = state.Execution.ActiveRouteId
            ?? throw new InvalidOperationException("Cannot complete without an active route.");
        var stepId = state.Execution.CurrentStepId
            ?? throw new InvalidOperationException("Cannot complete without a current step.");
        var route = state.Route(routeId);
        var steps = route.Steps
            .Select(step => step.Id == stepId ? step with { IsCompleted = true } : step)
            .ToArray();
        var updated = route with { Steps = steps };
        var nextStep = updated.CurrentStep();
        if (nextStep is null)
        {
            var completed = state.Routes
                .Select(item => item.Id == routeId
                    ? updated with { Lifecycle = RouteLifecycle.Completed }
                    : item)
                .ToArray();
            return state.With(completed, new ExecutionState(null, null));
        }

        var active = state.Routes
            .Select(item => item.Id == routeId
                ? updated with { Lifecycle = RouteLifecycle.Active }
                : item)
            .ToArray();
        return state.With(active, new ExecutionState(routeId, nextStep.Id));
    }

    public static AppState StartFallback(AppState state)
    {
        if (state.Execution.Mode != ExecutionMode.Normal)
        {
            throw new InvalidOperationException("A fallback can only start from the normal current action.");
        }

        var routeId = state.Execution.ActiveRouteId
            ?? throw new InvalidOperationException("Cannot enter fallback without an active route.");
        var stepId = state.Execution.CurrentStepId
            ?? throw new InvalidOperationException("Cannot enter fallback without a current step.");
        var step = state.Route(routeId).Steps.Single(step => step.Id == stepId);
        if (step.FallbackAction is null)
        {
            throw new InvalidOperationException("The current step has no prepared fallback.");
        }

        return state.With(
            state.Routes,
            new ExecutionState(routeId, stepId, ExecutionMode.Fallback));
    }

    public static AppState CompleteFallback(AppState state)
    {
        if (state.Execution.Mode != ExecutionMode.Fallback)
        {
            throw new InvalidOperationException("No fallback is currently active.");
        }

        return state.With(
            state.Routes,
            state.Execution with { Mode = ExecutionMode.Normal });
    }
}
