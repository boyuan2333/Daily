using ExecutionContinuity.App;
using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class ProjectGroupingTests
{
    [Fact]
    public void Project_grouping_orders_projects_by_name_and_keeps_unassigned_last()
    {
        var alpha = Project.Create("Alpha");
        var beta = Project.Create("Beta");
        var alphaRoute = Route.Create("Alpha route", alpha.Id, new[] { Step.Create("A", "Done", "Boundary") });
        var betaRoute = Route.Create("Beta route", beta.Id, new[] { Step.Create("B", "Done", "Boundary") });
        var loose = Route.Create("Loose route", Step.Create("C", "Done", "Boundary"));
        var state = AppState.Restore(
            [alphaRoute, betaRoute, loose],
            new ExecutionState(null, null),
            [],
            [],
            projects: [beta, alpha]);

        var sections = RouteListPresentation.GroupByProject(state);

        Assert.Equal(
            ["Alpha", "Beta", RouteListPresentation.UnassignedSectionTitle],
            sections.Select(section => section.Title));
        Assert.Equal("Loose route", sections[2].Routes.Single().Title);
    }

    [Fact]
    public void Project_grouping_orders_routes_inside_a_group_by_lifecycle()
    {
        var project = Project.Create("Project A");
        var active = Route.Create("Active", project.Id, new[] { Step.Create("A", "Done", "Boundary") }) with
        {
            Lifecycle = RouteLifecycle.Active
        };
        var paused = Route.Create("Paused", project.Id, new[] { Step.Create("B", "Done", "Boundary") }) with
        {
            Lifecycle = RouteLifecycle.Paused
        };
        var draft = Route.Create("Draft", project.Id, new[] { Step.Create("C", "Done", "Boundary") });
        var state = AppState.Restore(
            [draft, paused, active],
            new ExecutionState(active.Id, active.Steps[0].Id),
            [],
            [],
            projects: [project]);

        var section = Assert.Single(RouteListPresentation.GroupByProject(state));

        Assert.Equal(["Active", "Paused", "Draft"], section.Routes.Select(route => route.Title));
    }

    [Fact]
    public void Route_search_matches_the_project_name()
    {
        var project = Project.Create("Graduation recording");
        var route = Route.Create(
            "Prepare the session",
            project.Id,
            new[] { Step.Create("Check input", "Input checked", "Do not record yet") });
        var state = AppState.Restore(
            [route],
            new ExecutionState(null, null),
            [],
            [],
            projects: [project]);

        Assert.Contains(route, RouteListPresentation.Search(state, "graduation"));
        Assert.Empty(RouteListPresentation.Search(state, "unrelated"));
    }

    [Fact]
    public void Project_grouping_omits_projects_without_visible_routes_without_deleting_them()
    {
        var empty = Project.Create("Empty");
        var used = Project.Create("Used");
        var route = Route.Create("Route", used.Id, new[] { Step.Create("A", "Done", "Boundary") });
        var state = AppState.Restore(
            [route],
            new ExecutionState(null, null),
            [],
            [],
            projects: [empty, used]);

        var sections = RouteListPresentation.GroupByProject(state);

        Assert.Equal(["Used"], sections.Select(section => section.Title));
        Assert.Equal(2, state.Projects.Count);
    }
}
