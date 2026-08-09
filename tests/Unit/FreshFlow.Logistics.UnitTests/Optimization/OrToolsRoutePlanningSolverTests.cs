using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Infrastructure.Optimization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Optimization;

[Trait("Category", "Unit")]
public sealed class OrToolsRoutePlanningSolverTests
{
    [Fact]
    public void Solve_BinPackingTrap_UsesTwoVehiclesWithoutSplittingRestaurants()
    {
        var settings = Substitute.For<IVehicleCapacityPolicy>();
        settings.MaxStopsPerVehicle.Returns(20);
        settings.SolverTimeLimitSeconds.Returns(3);
        settings.CostPerKm.Returns(5000m);
        var solver = new OrToolsRoutePlanningSolver(
            settings, NullLogger<OrToolsRoutePlanningSolver>.Instance);
        var demands = new[] { 60m, 50m, 40m, 50m }.Select((load, index) =>
            new RestaurantDemand(Guid.NewGuid(), $"R{index}", [Guid.NewGuid()],
                10m + index, 106m, load)).ToList();
        var vehicles = Enumerable.Range(0, 2).Select(index => new PlanningVehicle(
            Guid.NewGuid(), $"51A-{index:00000}", VehicleType.truck, "car", 111.12m, 100m)).ToList();
        var matrix = Enumerable.Range(0, 5)
            .Select(from => Enumerable.Range(0, 5).Select(to => from == to ? 0L : 1000L).ToArray())
            .ToArray();
        var input = new RoutePlanningInput(
            Guid.NewGuid(), "Hub", 10m, 106m, new DateOnly(2026, 8, 10),
            demands, vehicles, "revision");

        var result = solver.Solve(input,
            new RouteMatrixResult(new Dictionary<string, ProfileRouteMatrix>
            {
                ["car"] = new(matrix, matrix)
            }, "TEST", false, []), OptimizationCriteria.distance);

        result.Unassigned.Should().BeEmpty();
        result.Routes.Should().HaveCount(2);
        result.Routes.Should().OnlyContain(route => route.Restaurants.Sum(x => x.LoadKg) <= 100m);
        result.Routes.SelectMany(x => x.Restaurants).Select(x => x.RestaurantId)
            .Should().BeEquivalentTo(demands.Select(x => x.RestaurantId));
        result.Routes.Should().OnlyContain(route => route.DistanceMeters >= 3000);
    }
}
