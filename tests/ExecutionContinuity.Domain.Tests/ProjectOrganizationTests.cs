using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class ProjectOrganizationTests
{
    [Fact]
    public void Assigning_a_project_creates_it_once_and_reuses_it_by_name()
    {
        var first = Route.Create("First", Step.Create("Action", "Done", "Boundary"));
        var second = Route.Create("Second", Step.Create("Action", "Done", "Boundary"));
        var state = AppState.Create(first, second);

        state = StateTransitions.SetRouteProject(state, first.Id, "Graduation recording");
        state = StateTransitions.SetRouteProject(state, second.Id, "graduation RECORDING");

        var project = Assert.Single(state.Projects);
        Assert.Equal("Graduation recording", project.Name);
        Assert.Equal(project.Id, state.Route(first.Id).ProjectId);
        Assert.Equal(project.Id, state.Route(second.Id).ProjectId);
    }

    [Fact]
    public void Assigning_an_empty_project_name_unassigns_without_deleting_the_project()
    {
        var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
        var assigned = StateTransitions.SetRouteProject(AppState.Create(route), route.Id, "Project A");

        var cleared = StateTransitions.SetRouteProject(assigned, route.Id, "   ");

        Assert.Null(cleared.Route(route.Id).ProjectId);
        Assert.Single(cleared.Projects);
    }

    [Fact]
    public void Assigning_a_project_never_changes_execution_state_steps_or_snapshots()
    {
        var step = Step.Create("Action", "Done", "Boundary");
        var route = Route.Create("Route", step);
        var active = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
        var paused = StateTransitions.Pause(
            active,
            new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero),
            "note");

        var assigned = StateTransitions.SetRouteProject(paused, route.Id, "Project A");

        Assert.Equal(paused.Execution, assigned.Execution);
        Assert.Equal(paused.Snapshots, assigned.Snapshots);
        Assert.Equal(paused.Captures, assigned.Captures);
        Assert.Equal(paused.Route(route.Id).Steps, assigned.Route(route.Id).Steps);
        Assert.Equal(paused.Route(route.Id).Lifecycle, assigned.Route(route.Id).Lifecycle);
    }

    [Fact]
    public void A_route_cannot_reference_a_project_that_does_not_exist()
    {
        var orphan = Route.Create("Orphan", Step.Create("Action", "Done", "Boundary")) with
        {
            ProjectId = Guid.NewGuid()
        };

        Assert.Throws<InvalidOperationException>(() => AppState.Create(orphan));
    }

    [Fact]
    public void A_new_route_can_be_created_directly_inside_a_project()
    {
        var project = Project.Create("Project A");
        var route = Route.Create("Route", project.Id, new[] { Step.Create("Action", "Done", "Boundary") });

        Assert.Equal(project.Id, route.ProjectId);
        Assert.Equal(RouteLifecycle.Draft, route.Lifecycle);
        Assert.Equal(0, route.Steps.Single().Position);
    }
}
