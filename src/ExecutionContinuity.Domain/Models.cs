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

public enum LanguagePreference
{
    FollowSystem,
    SimplifiedChinese,
    English
}

public sealed record Project(Guid Id, string Name)
{
    public static Project Create(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A project name cannot be empty.", nameof(name));
        }

        return new Project(Guid.NewGuid(), trimmed);
    }
}

public sealed record TaskFlowStep(
    Guid Id,
    int Position,
    string Action,
    string CompletionStandard,
    string DoNotDo,
    string? FallbackAction)
{
    public static TaskFlowStep Create(
        string action,
        string completionStandard,
        string doNotDo,
        string? fallbackAction = null) =>
        new(Guid.NewGuid(), 0, action, completionStandard, doNotDo, fallbackAction);
}

public sealed record TaskFlow(
    Guid Id,
    string Title,
    IReadOnlyList<TaskFlowStep> Steps,
    Guid? ProjectId = null)
{
    public static TaskFlow Create(string title, IEnumerable<TaskFlowStep> steps)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A task flow title cannot be empty.", nameof(title));
        }

        var ordered = steps.Select((step, index) => step with { Position = index }).ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("A task flow requires at least one step.", nameof(steps));
        }

        return new TaskFlow(Guid.NewGuid(), trimmed, ordered);
    }
}

public sealed record Step(
    Guid Id,
    int Position,
    string Action,
    string CompletionStandard,
    string DoNotDo,
    string? FallbackAction,
    bool IsCompleted,
    DateOnly? PlannedDate = null,
    TimeOnly? PlannedTime = null)
{
    public static Step Create(
        string action,
        string completionStandard,
        string doNotDo,
        string? fallbackAction = null,
        DateOnly? plannedDate = null,
        TimeOnly? plannedTime = null) =>
        new(Guid.NewGuid(), 0, action, completionStandard, doNotDo, fallbackAction, false, plannedDate, plannedTime);
}

public sealed record Route(
    Guid Id,
    string Title,
    IReadOnlyList<Step> Steps,
    RouteLifecycle Lifecycle,
    Guid? ProjectId = null,
    Guid? SourceTaskFlowId = null)
{
    public static Route Create(string title, params Step[] steps) => Create(title, null, steps);

    public static Route Create(string title, Guid? projectId, IEnumerable<Step> steps) =>
        new(
            Guid.NewGuid(),
            title,
            steps.Select((step, index) => step with { Position = index }).ToArray(),
            RouteLifecycle.Draft,
            projectId);

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

public sealed record CaptureEntry(
    Guid Id,
    string RawText,
    DateTimeOffset CapturedAt,
    bool IsArchived = false,
    string? OrganizedText = null);

public enum HistoryEventKind
{
    RouteStarted,
    RouteResumed,
    StepCompleted,
    RouteCompleted,
    Paused
}

public sealed record HistoryEvent(
    Guid Id,
    HistoryEventKind Kind,
    Guid RouteId,
    Guid? StepId,
    string Summary,
    DateTimeOffset OccurredAt)
{
    public static HistoryEvent Create(
        HistoryEventKind kind,
        Guid routeId,
        Guid? stepId,
        string summary,
        DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), kind, routeId, stepId, summary ?? string.Empty, occurredAt);
}

public sealed record ExecutionState(
    Guid? ActiveRouteId,
    Guid? CurrentStepId,
    ExecutionMode Mode = ExecutionMode.Normal);

public sealed class AppState
{
    private readonly IReadOnlyList<Route> _routes;
    private readonly IReadOnlyList<ExecutionSnapshot> _snapshots;
    private readonly IReadOnlyList<CaptureEntry> _captures;
    private readonly IReadOnlyList<Project> _projects;
    private readonly IReadOnlyList<HistoryEvent> _history;
    private readonly IReadOnlyList<TaskFlow> _taskFlows;

    private AppState(
        IReadOnlyList<Route> routes,
        ExecutionState execution,
        IReadOnlyList<ExecutionSnapshot> snapshots,
        IReadOnlyList<CaptureEntry> captures,
        LanguagePreference languagePreference,
        IReadOnlyList<Project> projects,
        IReadOnlyList<HistoryEvent> history,
        IReadOnlyList<TaskFlow> taskFlows)
    {
        _routes = routes;
        Execution = execution;
        _snapshots = snapshots;
        _captures = captures;
        LanguagePreference = languagePreference;
        _projects = projects;
        _history = history;
        _taskFlows = taskFlows;
    }

    public IReadOnlyList<Route> Routes => _routes;

    public ExecutionState Execution { get; }

    public IReadOnlyList<ExecutionSnapshot> Snapshots => _snapshots;

    public IReadOnlyList<CaptureEntry> Captures => _captures;

    public LanguagePreference LanguagePreference { get; }

    public IReadOnlyList<Project> Projects => _projects;

    public IReadOnlyList<HistoryEvent> History => _history;

    public IReadOnlyList<TaskFlow> TaskFlows => _taskFlows;

    public static AppState Create(params Route[] routes)
    {
        var state = new AppState(
            routes.ToArray(),
            new ExecutionState(null, null),
            Array.Empty<ExecutionSnapshot>(),
            Array.Empty<CaptureEntry>(),
            LanguagePreference.FollowSystem,
            Array.Empty<Project>(),
            Array.Empty<HistoryEvent>(),
            Array.Empty<TaskFlow>());
        state.ValidateInvariants();
        return state;
    }

    public static AppState Restore(
        IEnumerable<Route> routes,
        ExecutionState execution,
        IEnumerable<ExecutionSnapshot> snapshots,
        IEnumerable<CaptureEntry> captures,
        LanguagePreference languagePreference = LanguagePreference.FollowSystem,
        IEnumerable<Project>? projects = null,
        IEnumerable<HistoryEvent>? history = null,
        IEnumerable<TaskFlow>? taskFlows = null)
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
            captures.ToArray(),
            languagePreference,
            projects?.ToArray() ?? Array.Empty<Project>(),
            history?.ToArray() ?? Array.Empty<HistoryEvent>(),
            taskFlows?.ToArray() ?? Array.Empty<TaskFlow>());
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
        IReadOnlyList<CaptureEntry>? captures = null,
        LanguagePreference? languagePreference = null,
        IReadOnlyList<Project>? projects = null,
        IReadOnlyList<HistoryEvent>? history = null,
        IReadOnlyList<TaskFlow>? taskFlows = null)
    {
        var next = new AppState(
            routes,
            execution,
            snapshots ?? Snapshots,
            captures ?? Captures,
            languagePreference ?? LanguagePreference,
            projects ?? Projects,
            history ?? History,
            taskFlows ?? TaskFlows);
        next.ValidateInvariants();
        return next;
    }

    public void ValidateInvariants()
    {
        if (_projects.Select(project => project.Id).Distinct().Count() != _projects.Count)
        {
            throw new InvalidOperationException("Project IDs must be unique.");
        }

        foreach (var route in _routes)
        {
            if (route.ProjectId is Guid projectId && _projects.All(project => project.Id != projectId))
            {
                throw new InvalidOperationException("A route can only reference a project that exists.");
            }
        }

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

    public static AppState AddTaskFlow(AppState state, TaskFlow flow)
    {
        if (state.TaskFlows.Any(existing => existing.Id == flow.Id))
        {
            throw new InvalidOperationException("A task flow with the same ID already exists.");
        }

        if (string.IsNullOrWhiteSpace(flow.Title) || flow.Steps.Count == 0)
        {
            throw new ArgumentException("A task flow requires a title and at least one step.", nameof(flow));
        }

        return state.With(
            state.Routes,
            state.Execution,
            taskFlows: state.TaskFlows.Append(flow).ToArray());
    }

    public static AppState CreateTaskFlowFromRoute(AppState state, Guid routeId, string title)
    {
        var route = state.Route(routeId);
        var flow = TaskFlow.Create(
            title,
            route.Steps.Select(step => TaskFlowStep.Create(
                step.Action,
                step.CompletionStandard,
                step.DoNotDo,
                step.FallbackAction)));
        return AddTaskFlow(state, flow);
    }

    public static AppState StartTaskFlow(
        AppState state,
        Guid taskFlowId,
        DateTimeOffset startedAt,
        string? note = null)
    {
        var flow = state.TaskFlows.SingleOrDefault(item => item.Id == taskFlowId)
            ?? throw new InvalidOperationException("The task flow does not exist.");

        var unfinished = state.Routes
            .Where(route => route.SourceTaskFlowId == flow.Id)
            .FirstOrDefault(route => route.Lifecycle is
                RouteLifecycle.Draft or RouteLifecycle.Active or RouteLifecycle.Paused);
        if (unfinished is not null)
        {
            return state.Execution.ActiveRouteId == unfinished.Id
                ? state
                : SelectActiveRoute(state, unfinished.Id, startedAt, note);
        }

        var instance = new Route(
            Guid.NewGuid(),
            flow.Title,
            flow.Steps
                .OrderBy(step => step.Position)
                .Select((step, index) => new Step(
                    Guid.NewGuid(),
                    index,
                    step.Action,
                    step.CompletionStandard,
                    step.DoNotDo,
                    step.FallbackAction,
                    false))
                .ToArray(),
            RouteLifecycle.Draft,
            flow.ProjectId,
            flow.Id);

        return SelectActiveRoute(AddRoute(state, instance), instance.Id, startedAt, note);
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

    public static AppState SetStepDate(
        AppState state,
        Guid routeId,
        Guid stepId,
        DateOnly? plannedDate,
        TimeOnly? plannedTime)
    {
        var route = state.Route(routeId);
        if (route.Steps.All(step => step.Id != stepId))
        {
            throw new InvalidOperationException("The step does not belong to the route.");
        }

        if (plannedDate is null && plannedTime is not null)
        {
            throw new ArgumentException("A planned time requires a planned date.", nameof(plannedTime));
        }

        return state.With(
            state.Routes
                .Select(item => item.Id == routeId
                    ? item with
                    {
                        Steps = item.Steps
                            .Select(step => step.Id == stepId
                                ? step with { PlannedDate = plannedDate, PlannedTime = plannedTime }
                                : step)
                            .ToArray()
                    }
                    : item)
                .ToArray(),
            state.Execution);
    }

    public static AppState SetRouteProject(AppState state, Guid routeId, string? projectName)
    {
        state.Route(routeId);
        var normalized = string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        if (normalized is null)
        {
            return state.With(
                state.Routes
                    .Select(item => item.Id == routeId ? item with { ProjectId = null } : item)
                    .ToArray(),
                state.Execution);
        }

        var project = state.Projects.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, normalized, StringComparison.OrdinalIgnoreCase));
        var projects = state.Projects;
        if (project is null)
        {
            project = Project.Create(normalized);
            projects = projects.Append(project).ToArray();
        }

        return state.With(
            state.Routes
                .Select(item => item.Id == routeId ? item with { ProjectId = project.Id } : item)
                .ToArray(),
            state.Execution,
            projects: projects);
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
        var history = state.History;
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
            history = history.Append(HistoryEvent.Create(
                HistoryEventKind.Paused,
                previousRoute.Id,
                previousStep.Id,
                previousRoute.Title,
                switchedAt.Value)).ToArray();
        }

        if (switchedAt is not null && selected.Lifecycle is RouteLifecycle.Draft or RouteLifecycle.Paused)
        {
            history = history.Append(HistoryEvent.Create(
                selected.Lifecycle == RouteLifecycle.Paused
                    ? HistoryEventKind.RouteResumed
                    : HistoryEventKind.RouteStarted,
                selected.Id,
                selected.CurrentStep()?.Id,
                selected.Title,
                switchedAt.Value)).ToArray();
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
            snapshots,
            history: history);
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

    public static AppState OrganizeCapture(AppState state, Guid captureId, string? organizedText)
    {
        if (state.Captures.All(capture => capture.Id != captureId))
        {
            throw new InvalidOperationException("The capture does not exist.");
        }

        var normalized = string.IsNullOrWhiteSpace(organizedText) ? null : organizedText;
        return state.With(
            state.Routes,
            state.Execution,
            captures: state.Captures
                .Select(capture => capture.Id == captureId
                    ? capture with { OrganizedText = normalized }
                    : capture)
                .ToArray());
    }

    public static AppState SetLanguagePreference(AppState state, LanguagePreference preference) =>
        state.With(state.Routes, state.Execution, languagePreference: preference);

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
            state.Snapshots.Append(snapshot).ToArray(),
            history: state.History.Append(HistoryEvent.Create(
                HistoryEventKind.Paused,
                route.Id,
                step.Id,
                route.Title,
                pausedAt)).ToArray());
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

    public static AppState CompleteCurrentStep(AppState state, DateTimeOffset completedAt)
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
        var step = route.Steps.Single(candidate => candidate.Id == stepId);
        var steps = route.Steps
            .Select(candidate => candidate.Id == stepId ? candidate with { IsCompleted = true } : candidate)
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
            var history = state.History
                .Append(HistoryEvent.Create(
                    HistoryEventKind.StepCompleted,
                    routeId,
                    stepId,
                    step.Action,
                    completedAt))
                .Append(HistoryEvent.Create(
                    HistoryEventKind.RouteCompleted,
                    routeId,
                    stepId,
                    route.Title,
                    completedAt))
                .ToArray();
            return state.With(completed, new ExecutionState(null, null), history: history);
        }

        var active = state.Routes
            .Select(item => item.Id == routeId
                ? updated with { Lifecycle = RouteLifecycle.Active }
                : item)
            .ToArray();
        return state.With(
            active,
            new ExecutionState(routeId, nextStep.Id),
            history: state.History.Append(HistoryEvent.Create(
                HistoryEventKind.StepCompleted,
                routeId,
                stepId,
                step.Action,
                completedAt)).ToArray());
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
