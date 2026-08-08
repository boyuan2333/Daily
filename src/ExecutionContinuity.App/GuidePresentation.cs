using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public enum GuideScreen
{
    NoActiveRoute,
    CurrentAction,
    Fallback,
    Blocked
}
public sealed record GuidePresentation(
    GuideScreen Screen,
    string? RouteTitle,
    string? StepProgress,
    string? Action,
    string? CompletionStandard,
    string? DoNotDo,
    bool CanCapture,
    bool CanPause,
    bool CanCompleteCurrentStep,
    bool CanStartFallback,
    bool CanCompleteFallback,
    bool CanReturnFromBlocked)
{
    public static GuidePresentation From(AppState state)
    {
        if (state.Execution.ActiveRouteId is not Guid routeId ||
            state.Execution.CurrentStepId is not Guid stepId)
        {
            return new(
                GuideScreen.NoActiveRoute,
                null,
                null,
                null,
                null,
                null,
                CanCapture: true,
                CanPause: false,
                CanCompleteCurrentStep: false,
                CanStartFallback: false,
                CanCompleteFallback: false,
                CanReturnFromBlocked: false);
        }

        var step = state.Route(routeId).Steps.Single(candidate => candidate.Id == stepId);
        var route = state.Route(routeId);
        var stepProgress = $"{route.Steps.Count(candidate => candidate.IsCompleted) + 1}/{route.Steps.Count} 步";
        return state.Execution.Mode switch
        {
            ExecutionMode.Fallback => new(
                GuideScreen.Fallback,
                route.Title,
                stepProgress,
                step.FallbackAction,
                null,
                null,
                CanCapture: true,
                CanPause: false,
                CanCompleteCurrentStep: false,
                CanStartFallback: false,
                CanCompleteFallback: true,
                CanReturnFromBlocked: false),
            ExecutionMode.Blocked => new(
                GuideScreen.Blocked,
                route.Title,
                stepProgress,
                null,
                null,
                null,
                CanCapture: false,
                CanPause: true,
                CanCompleteCurrentStep: false,
                CanStartFallback: false,
                CanCompleteFallback: false,
                CanReturnFromBlocked: true),
            _ => new(
                GuideScreen.CurrentAction,
                route.Title,
                stepProgress,
                step.Action,
                step.CompletionStandard,
                step.DoNotDo,
                CanCapture: true,
                CanPause: true,
                CanCompleteCurrentStep: true,
                CanStartFallback: step.FallbackAction is not null,
                CanCompleteFallback: false,
                CanReturnFromBlocked: false)
        };
    }
}
