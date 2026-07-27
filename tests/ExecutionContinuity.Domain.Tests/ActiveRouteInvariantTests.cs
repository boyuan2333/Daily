using ExecutionContinuity.Domain;
using Xunit;

namespace ExecutionContinuity.Domain.Tests;

public sealed class ActiveRouteInvariantTests
{
    [Fact]
    public void Selecting_a_route_makes_it_the_only_active_route()
    {
        var routeA = Route.Create("Route A", Step.Create("Action A", "Done A", "Boundary A"));
        var routeB = Route.Create("Route B", Step.Create("Action B", "Done B", "Boundary B"));
        var state = AppState.Create(routeA, routeB);

        var selected = StateTransitions.SelectActiveRoute(state, routeB.Id);

        Assert.Equal(routeB.Id, selected.Execution.ActiveRouteId);
        Assert.Equal(routeB.Steps[0].Id, selected.Execution.CurrentStepId);
        Assert.Equal(RouteLifecycle.Active, selected.Route(routeB.Id).Lifecycle);
        Assert.NotEqual(RouteLifecycle.Active, selected.Route(routeA.Id).Lifecycle);
        selected.ValidateInvariants();
    }
}
