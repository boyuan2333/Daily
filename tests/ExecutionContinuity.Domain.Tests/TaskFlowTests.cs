using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class TaskFlowTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Starting_a_task_flow_creates_one_instance_and_activates_it()
    {
        var flow = TaskFlow.Create("Morning routine", new[]
        {
            TaskFlowStep.Create("Wash face", "Face is clean", "Do not rush"),
            TaskFlowStep.Create("Brush teeth", "Two minutes done", "Do not skip")
        });
        var state = StateTransitions.AddTaskFlow(AppState.Create(), flow);

        var started = StateTransitions.StartTaskFlow(state, flow.Id, StartedAt);

        var route = Assert.Single(started.Routes);
        Assert.Equal("Morning routine", route.Title);
        Assert.Equal(flow.Id, route.SourceTaskFlowId);
        Assert.Equal(RouteLifecycle.Active, route.Lifecycle);
        Assert.Equal(route.Id, started.Execution.ActiveRouteId);
        Assert.Equal(["Wash face", "Brush teeth"], route.Steps.Select(step => step.Action));
        Assert.Equal([0, 1], route.Steps.Select(step => step.Position));
        Assert.Single(started.TaskFlows);
    }

    [Fact]
    public void Starting_a_flow_with_an_unfinished_instance_resumes_it_without_a_self_snapshot()
    {
        var flow = TaskFlow.Create("Morning routine", new[] { TaskFlowStep.Create("Wash face", "Done", "Boundary") });
        var started = StateTransitions.StartTaskFlow(
            StateTransitions.AddTaskFlow(AppState.Create(), flow),
            flow.Id,
            StartedAt);
        var routeId = started.Routes.Single().Id;
        var elsewhere = StateTransitions.AddRoute(
            started,
            Route.Create("Other", Step.Create("Other action", "Done", "Boundary")));

        var restarted = StateTransitions.StartTaskFlow(elsewhere, flow.Id, StartedAt.AddMinutes(10));

        Assert.Single(restarted.Routes, item => item.SourceTaskFlowId == flow.Id);
        Assert.Equal(routeId, restarted.Execution.ActiveRouteId);
        Assert.Empty(restarted.Snapshots);
    }

    [Fact]
    public void Adding_an_edited_flow_never_rewrites_an_existing_instance()
    {
        var flow = TaskFlow.Create("Morning routine", new[] { TaskFlowStep.Create("Wash face", "Done", "Boundary") });
        var started = StateTransitions.StartTaskFlow(
            StateTransitions.AddTaskFlow(AppState.Create(), flow),
            flow.Id,
            StartedAt);
        var instanceSteps = started.Routes.Single().Steps;
        var edited = flow with
        {
            Id = Guid.NewGuid(),
            Steps = new[] { TaskFlowStep.Create("Different", "Done", "Boundary") }
        };

        var withEdited = StateTransitions.AddTaskFlow(started, edited);

        Assert.Equal(instanceSteps, withEdited.Routes.Single().Steps);
        Assert.Equal(2, withEdited.TaskFlows.Count);
    }

    [Fact]
    public void Creating_a_flow_from_a_route_copies_its_steps_without_activating_anything()
    {
        var route = Route.Create("Prepare the session", Step.Create("Check input", "Input checked", "Do not record yet"));
        var state = AppState.Create(route);

        var withFlow = StateTransitions.CreateTaskFlowFromRoute(state, route.Id, "Prepare the session");

        var flow = Assert.Single(withFlow.TaskFlows);
        Assert.Equal("Prepare the session", flow.Title);
        Assert.Equal("Check input", flow.Steps.Single().Action);
        Assert.Equal("Input checked", flow.Steps.Single().CompletionStandard);
        Assert.DoesNotContain(withFlow.Routes, item => item.SourceTaskFlowId == flow.Id);
        Assert.Null(withFlow.Execution.ActiveRouteId);
    }

    [Fact]
    public void A_task_flow_requires_a_title_and_at_least_one_step()
    {
        Assert.Throws<ArgumentException>(() =>
            TaskFlow.Create("  ", new[] { TaskFlowStep.Create("A", "B", "C") }));
        Assert.Throws<ArgumentException>(() =>
            TaskFlow.Create("Empty", Array.Empty<TaskFlowStep>()));
    }
}
