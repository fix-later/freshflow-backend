using FluentAssertions;
using FreshFlow.Logistics.Application.Common;

namespace FreshFlow.Logistics.UnitTests.Common;

[Trait("Category", "Unit")]
public sealed class CapacitatedSweepPlannerTests
{
    [Fact]
    public void Plan_ThreeRestaurantsFitOneVehicle_ReturnsOneCluster()
    {
        // Arrange
        var restaurants = Restaurants(20m, 30m, 40m);
        var vehicles = new[] { new SweepVehicleCapacity(100m, 4) };

        // Act
        var result = CapacitatedSweepPlanner.Plan(0m, 0m, restaurants, vehicles);

        // Assert
        result.Clusters.Should().ContainSingle();
        result.Clusters[0].Restaurants.Should().HaveCount(3);
        result.Unassignable.Should().BeEmpty();
    }

    [Fact]
    public void Plan_LoadExceedsOneVehicleButFitsTwo_ReturnsTwoValidClusters()
    {
        // Arrange
        var restaurants = Restaurants(60m, 60m, 40m);
        var vehicles = new[]
        {
            new SweepVehicleCapacity(100m, 4),
            new SweepVehicleCapacity(100m, 4)
        };

        // Act
        var result = CapacitatedSweepPlanner.Plan(0m, 0m, restaurants, vehicles);

        // Assert
        result.Clusters.Should().HaveCount(2);
        result.Clusters.Should().OnlyContain(cluster =>
            cluster.Restaurants.Sum(restaurant => restaurant.LoadKg) <= cluster.CapacityKg);
        result.Unassignable.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RestaurantHeavierThanEveryVehicle_ReturnsItAsUnassignable()
    {
        // Arrange
        var restaurants = Restaurants(101m);
        var vehicles = new[]
        {
            new SweepVehicleCapacity(100m, 4),
            new SweepVehicleCapacity(80m, 4)
        };

        // Act
        var result = CapacitatedSweepPlanner.Plan(0m, 0m, restaurants, vehicles);

        // Assert
        result.Clusters.Should().BeEmpty();
        result.Unassignable.Should().ContainSingle()
            .Which.RestaurantId.Should().Be(restaurants[0].RestaurantId);
    }

    [Fact]
    public void Plan_TotalLoadExceedsFleet_ReturnsOverflow()
    {
        // Arrange
        var restaurants = Restaurants(60m, 60m);
        var vehicles = new[] { new SweepVehicleCapacity(100m, 3) };

        // Act
        var result = CapacitatedSweepPlanner.Plan(0m, 0m, restaurants, vehicles);

        // Assert
        result.Clusters.Should().ContainSingle();
        result.Unassignable.Should().ContainSingle();
    }

    private static IReadOnlyList<SweepRestaurant> Restaurants(params decimal[] loads) =>
        loads.Select((load, index) => new SweepRestaurant(
                Guid.NewGuid(),
                index + 1,
                1m,
                load))
            .ToList();
}
