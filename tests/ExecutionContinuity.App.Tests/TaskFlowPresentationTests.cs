using ExecutionContinuity.App;
using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class TaskFlowPresentationTests
{
    [Fact]
    public void Flows_are_listed_alphabetically_and_report_an_unfinished_instance()
    {
        var morning = TaskFlow.Create("Morning routine", new[] { TaskFlowStep.Create("Wash face", "Done", "Boundary") });
        var evening = TaskFlow.Create("Evening routine", new[] { TaskFlowStep.Create("Floss", "Done", "Boundary") });
        var state = StateTransitions.AddTaskFlow(AppState.Create(), morning);
        state = StateTransitions.AddTaskFlow(state, evening);

        var summaries = TaskFlowPresentation.Flows(state);

        Assert.Equal(["Evening routine", "Morning routine"], summaries.Select(item => item.Flow.Title));
        Assert.All(summaries, item => Assert.False(item.HasUnfinishedInstance));
        Assert.All(summaries, item => Assert.Equal(1, item.StepCount));

        var started = StateTransitions.StartTaskFlow(
            state,
            morning.Id,
            new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero));
        var afterStart = TaskFlowPresentation.Flows(started);
        var resumed = afterStart.Single(item => item.Flow.Id == morning.Id);

        Assert.True(resumed.HasUnfinishedInstance);
        Assert.NotNull(resumed.InstanceRouteId);
    }

    [Fact]
    public void Task_flow_surface_exposes_the_group_actions_inside_the_routes_section()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"TaskFlowsWorkspace\"", xaml);
        Assert.Contains("x:Name=\"TaskFlowsPanel\"", xaml);
        Assert.Contains("x:Name=\"RoutesListViewButton\"", xaml);
        Assert.Contains("x:Name=\"TaskFlowsListViewButton\"", xaml);
        Assert.Contains("TaskFlowPresentation.Flows", code);
        Assert.Contains("StartTaskFlowButton_Click", code);
        Assert.Contains("SaveTaskFlowFromRouteButton_Click", code);
        Assert.Contains("RoutesSectionView.TaskFlows", code);
    }

    [Fact]
    public async Task Task_flows_and_their_instances_survive_store_recreation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"execution-continuity-flow-{Guid.NewGuid():N}.db");
        try
        {
            var flow = TaskFlow.Create("Morning routine", new[]
            {
                TaskFlowStep.Create("Wash face", "Face is clean", "Do not rush"),
                TaskFlowStep.Create("Brush teeth", "Two minutes done", "Do not skip")
            });
            var state = StateTransitions.StartTaskFlow(
                StateTransitions.AddTaskFlow(AppState.Create(), flow),
                flow.Id,
                new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero));

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            var recoveredFlow = Assert.Single(recovered.TaskFlows);
            Assert.Equal("Morning routine", recoveredFlow.Title);
            Assert.Equal(["Wash face", "Brush teeth"], recoveredFlow.Steps.Select(step => step.Action));
            var instance = Assert.Single(recovered.Routes);
            Assert.Equal(recoveredFlow.Id, instance.SourceTaskFlowId);
            Assert.Equal(instance.Id, recovered.Execution.ActiveRouteId);
        }
        finally
        {
            foreach (var candidate in new[] { path, $"{path}-wal", $"{path}-shm" })
            {
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
        }
    }
}
