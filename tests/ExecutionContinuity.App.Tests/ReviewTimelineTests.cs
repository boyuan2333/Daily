using ExecutionContinuity.App;
using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class ReviewTimelineTests
{
    private static readonly DateTimeOffset Base = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Timeline_lists_recorded_facts_newest_first()
    {
        var route = Route.Create("Prepare the session", Step.Create("Check input", "Done", "Boundary"));
        var started = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, Base);
        var paused = StateTransitions.Pause(started, Base.AddMinutes(5), "note");

        var timeline = ReviewPresentation.Timeline(paused);

        Assert.Equal(
            [HistoryEventKind.Paused, HistoryEventKind.RouteStarted],
            timeline.Select(entry => entry.Kind));
        Assert.Equal(Base.AddMinutes(5), timeline[0].OccurredAt);
        Assert.Equal("Prepare the session", timeline[0].Summary);
    }

    [Fact]
    public void Timeline_is_empty_without_recorded_history()
    {
        Assert.Empty(ReviewPresentation.Timeline(AppState.Create()));
    }

    [Fact]
    public void Timeline_never_derives_facts_from_snapshots_or_step_state()
    {
        var step = Step.Create("Action", "Done", "Boundary");
        var route = Route.Create("Route", step) with { Lifecycle = RouteLifecycle.Paused };
        var state = AppState.Restore(
            [route],
            new ExecutionState(null, null),
            [
                new ExecutionSnapshot(
                    Guid.NewGuid(),
                    route.Id,
                    step.Id,
                    step.Action,
                    step.CompletionStandard,
                    step.DoNotDo,
                    step.FallbackAction,
                    Base,
                    "paused earlier")
            ],
            []);

        Assert.Empty(ReviewPresentation.Timeline(state));
    }

    [Fact]
    public void History_kind_labels_follow_the_language_preference()
    {
        Assert.Equal(
            "完成步骤",
            UiText.HistoryKindLabel(LanguagePreference.SimplifiedChinese, HistoryEventKind.StepCompleted));
        Assert.Equal(
            "Step completed",
            UiText.HistoryKindLabel(LanguagePreference.English, HistoryEventKind.StepCompleted));
        Assert.Equal(
            "暂停",
            UiText.HistoryKindLabel(LanguagePreference.SimplifiedChinese, HistoryEventKind.Paused));
    }

    [Fact]
    public void Review_surface_exposes_a_read_only_timeline_panel()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"ReviewTimelinePanel\"", xaml);
        Assert.Contains("x:Name=\"ReviewTimelineScrollViewer\"", xaml);
        Assert.Contains("ReviewPresentation.Timeline", code);
        Assert.DoesNotContain("ReviewPlaceholderText", xaml);
    }
}
