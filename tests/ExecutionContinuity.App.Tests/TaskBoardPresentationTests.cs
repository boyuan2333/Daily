using ExecutionContinuity.App;
using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class TaskBoardPresentationTests
{
    [Fact]
    public void Tasks_exclude_completed_steps_and_archived_routes()
    {
        var draft = Route.Create("Draft route", Step.Create("First", "Done", "Boundary"));
        var completed = Route.Create(
            "Completed route",
            Step.Create("Done already", "Done", "Boundary") with { IsCompleted = true });
        var archived = Route.Create("Archived route", Step.Create("Hidden", "Done", "Boundary")) with
        {
            Lifecycle = RouteLifecycle.Archived
        };
        var state = AppState.Restore([draft, completed, archived], new ExecutionState(null, null), [], []);

        var task = Assert.Single(TaskBoardPresentation.Tasks(state));

        Assert.Equal("First", task.Action);
        Assert.Equal("Draft route", task.RouteTitle);
    }

    [Fact]
    public void Tasks_report_steps_without_a_completion_standard_as_not_ready()
    {
        var ready = Route.Create("Ready", Step.Create("Prepared", "Done", "Boundary"));
        var notReady = Route.Create("Not ready", Step.Create("Rough idea", string.Empty, string.Empty));
        var state = AppState.Restore([ready, notReady], new ExecutionState(null, null), [], []);

        var tasks = TaskBoardPresentation.Tasks(state);

        Assert.True(tasks.Single(task => task.RouteTitle == "Ready").IsReady);
        Assert.False(tasks.Single(task => task.RouteTitle == "Not ready").IsReady);
    }

    [Fact]
    public void Tasks_follow_route_title_then_step_order()
    {
        var beta = Route.Create(
            "Beta",
            Step.Create("B1", "Done", "Boundary"),
            Step.Create("B2", "Done", "Boundary"));
        var alpha = Route.Create("Alpha", Step.Create("A1", "Done", "Boundary"));
        var state = AppState.Restore([beta, alpha], new ExecutionState(null, null), [], []);

        var tasks = TaskBoardPresentation.Tasks(state);

        Assert.Equal(["A1", "B1", "B2"], tasks.Select(task => task.Action));
    }

    [Fact]
    public void Calendar_buckets_sort_dated_tasks_ascending_and_keep_unscheduled_last()
    {
        var later = Step.Create("Later", "Done", "Boundary", plannedDate: new DateOnly(2026, 9, 25));
        var earlier = Step.Create("Earlier", "Done", "Boundary", plannedDate: new DateOnly(2026, 9, 20));
        var loose = Step.Create("Loose", "Done", "Boundary");
        var state = AppState.Create(Route.Create("Route", later, earlier, loose));

        var buckets = TaskBoardPresentation.CalendarBuckets(state);

        Assert.Equal(
            [new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25), null],
            buckets.Select(bucket => bucket.Date));
        Assert.Equal("Earlier", buckets[0].Tasks.Single().Action);
        Assert.Equal("Loose", buckets[2].Tasks.Single().Action);
    }

    [Fact]
    public void Calendar_buckets_place_the_all_day_row_before_timed_tasks()
    {
        var allDay = Step.Create("All day", "Done", "Boundary", plannedDate: new DateOnly(2026, 9, 22));
        var afternoon = Step.Create(
            "Afternoon",
            "Done",
            "Boundary",
            plannedDate: new DateOnly(2026, 9, 22),
            plannedTime: new TimeOnly(15, 0));
        var morning = Step.Create(
            "Morning",
            "Done",
            "Boundary",
            plannedDate: new DateOnly(2026, 9, 22),
            plannedTime: new TimeOnly(9, 30));
        var state = AppState.Create(Route.Create("Route", allDay, afternoon, morning));

        var bucket = Assert.Single(TaskBoardPresentation.CalendarBuckets(state));

        Assert.Equal(new DateOnly(2026, 9, 22), bucket.Date);
        Assert.Equal(["All day", "Morning", "Afternoon"], bucket.Tasks.Select(task => task.Action));
    }

    [Fact]
    public void Filter_matches_action_or_route_title_and_keeps_existing_order()
    {
        var tasks = new[]
        {
            new TaskProjection(
                Guid.NewGuid(),
                "Quarterly report",
                RouteLifecycle.Draft,
                Guid.NewGuid(),
                "Collect numbers",
                0,
                true,
                null,
                null),
            new TaskProjection(
                Guid.NewGuid(),
                "Morning routine",
                RouteLifecycle.Draft,
                Guid.NewGuid(),
                "Wash face",
                0,
                true,
                null,
                null)
        };

        Assert.Equal("Collect numbers", TaskBoardPresentation.Filter(tasks, "quarterly").Single().Action);
        Assert.Equal("Wash face", TaskBoardPresentation.Filter(tasks, "WASH").Single().Action);
        Assert.Equal(2, TaskBoardPresentation.Filter(tasks, "   ").Count);
        Assert.Empty(TaskBoardPresentation.Filter(tasks, "nothing"));
    }

    [Fact]
    public void Task_board_surface_exposes_calendar_list_search_and_step_dates()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"TaskBoardPanel\"", xaml);
        Assert.Contains("x:Name=\"TasksCalendarButton\"", xaml);
        Assert.Contains("x:Name=\"TasksListButton\"", xaml);
        Assert.Contains("x:Name=\"TaskSearchInput\"", xaml);
        Assert.Contains("x:Name=\"StepDatePicker\"", xaml);
        Assert.Contains("TaskBoardPresentation.Tasks", code);
        Assert.Contains("TaskBoardPresentation.CalendarBuckets", code);
        Assert.Contains("ApplyOrdinaryPlanningEntry", code);
        Assert.Contains("PlanningDestination.Tasks", code);
        Assert.DoesNotContain("TasksPlaceholderText", xaml);
    }
}
