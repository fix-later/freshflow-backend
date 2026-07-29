using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.PlanRoutes;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class PlanRoutesCommandHandlerTests
{
    [Fact]
    public async Task Handle_FleetCapacityExceeded_DoesNotAddAnyRouteAsync()
    {
        // Arrange
        var fixture = await CreateFixtureAsync([60m, 60m], [100m]);

        // Act
        var result = await fixture.Handler.Handle(
            new PlanRoutesCommand(fixture.HubId, fixture.ServiceDate, null),
            default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FLEET_CAPACITY_EXCEEDED");
        fixture.Routes.Routes.Should().BeEmpty();
        fixture.Routes.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesOnePlannedRoutePerClusterAsync()
    {
        // Arrange
        var fixture = await CreateFixtureAsync([60m, 60m, 40m], [100m, 100m]);

        // Act
        var result = await fixture.Handler.Handle(
            new PlanRoutesCommand(fixture.HubId, fixture.ServiceDate, "DISTANCE"),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        fixture.Routes.Routes.Should().HaveCount(2)
            .And.OnlyContain(route =>
                route.Status == RouteStatus.planned &&
                route.VehicleId == null &&
                route.DriverUserId == null &&
                route.OptimizationCriteria == OptimizationCriteria.distance);
        fixture.Routes.SaveChangesCount.Should().Be(1);
    }

    private static async Task<Fixture> CreateFixtureAsync(
        IReadOnlyList<decimal> restaurantLoads,
        IReadOnlyList<decimal> vehicleCapacities)
    {
        var hubId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 7, 30);
        var restaurantIds = restaurantLoads.Select(_ => Guid.NewGuid()).ToList();
        var orderIds = restaurantLoads.Select(_ => Guid.NewGuid()).ToList();
        var orders = Substitute.For<IOrderStatusReader>();
        orders.ListRoutableRestaurantsAsync(
                serviceDate,
                Arg.Is<IReadOnlyCollection<string>>(statuses => statuses.SequenceEqual(new[] { "AtHub" })),
                Arg.Any<CancellationToken>())
            .Returns(restaurantIds.Select(id => (id, 1)).ToList());
        orders.ListByRestaurantsAndStatusAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                "AtHub",
                Arg.Any<CancellationToken>(),
                hubId,
                serviceDate)
            .Returns(orderIds.Select((id, index) =>
                new OrderStatusLookupDto(id, "AtHub", restaurantIds[index], hubId)).ToList());

        var packing = Substitute.For<IOrderPackingReader>();
        packing.GetLinesByOrdersAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(orderIds.Select((id, index) => new OrderPackingLines(
                id,
                [new OrderPackingLine(id, Guid.NewGuid(), "Product", 1, restaurantLoads[index])]))
                .ToList());

        var hubs = Substitute.For<IHubCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, null, "Hub", 0m, 0m));
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        for (var index = 0; index < restaurantIds.Count; index++)
        {
            var restaurantId = restaurantIds[index];
            restaurants.FindByRestaurantIdAsync(restaurantId, Arg.Any<CancellationToken>())
                .Returns(new RestaurantCoordinateDto(
                    restaurantId,
                    $"Restaurant {index}",
                    index + 1,
                    1m));
        }

        var vehicles = new InMemoryVehicleRepository();
        for (var index = 0; index < vehicleCapacities.Count; index++)
        {
            await vehicles.AddAsync(
                new Vehicle($"51A-{index:00000}", vehicleCapacities[index], VehicleType.truck, null),
                default);
        }

        var optimizer = Substitute.For<IRouteOptimizer>();
        optimizer.Optimize(
                Arg.Any<IReadOnlyList<FreshFlow.Logistics.Domain.ValueObjects.RouteStop>>(),
                serviceDate,
                OptimizationCriteria.distance)
            .Returns(call =>
            {
                var stops = call.ArgAt<IReadOnlyList<FreshFlow.Logistics.Domain.ValueObjects.RouteStop>>(0);
                return new RouteOptimizationResult(stops, 10m, 20, 50_000m);
            });

        var capacityPolicy = Substitute.For<IVehicleCapacityPolicy>();
        capacityPolicy.MaxStopsPerVehicle.Returns(20);
        var routes = new InMemoryDeliveryRouteRepository();
        var handler = new PlanRoutesCommandHandler(
            hubs,
            restaurants,
            orders,
            packing,
            vehicles,
            capacityPolicy,
            optimizer,
            routes);

        return new Fixture(handler, routes, hubId, serviceDate);
    }

    private sealed record Fixture(
        PlanRoutesCommandHandler Handler,
        InMemoryDeliveryRouteRepository Routes,
        Guid HubId,
        DateOnly ServiceDate);
}
