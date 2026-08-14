using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Common;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Common;

[Trait("Category", "Unit")]
public sealed class RoutePlanningInputBuilderTests
{
    [Fact]
    public async Task BuildAsync_ExcludesReservedOrdersAndVehiclesAsync()
    {
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var reservedOrderId = Guid.NewGuid();
        var newOrderId = Guid.NewGuid();
        var reservedVehicle = new Vehicle("51A-00001", 100m, VehicleType.van, null);
        var freeVehicle = new Vehicle("51A-00002", 100m, VehicleType.van, null);
        var hubs = Substitute.For<IHubCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, null, "Hub", 10m, 106m));
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        restaurants.FindByRestaurantIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantCoordinateDto(restaurantId, "Restaurant", 10.1m, 106.1m));
        var orders = Substitute.For<IOrderStatusReader>();
        orders.ListRoutableRestaurantsAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([(restaurantId, 2)]);
        orders.ListByRestaurantsAndStatusAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), "AtHub", Arg.Any<CancellationToken>(),
                hubId, Arg.Any<DateOnly>())
            .Returns([
                new OrderStatusLookupDto(reservedOrderId, "AtHub", restaurantId, hubId),
                new OrderStatusLookupDto(newOrderId, "AtHub", restaurantId, hubId)
            ]);
        var packing = Substitute.For<IOrderPackingReader>();
        packing.GetLinesByOrdersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new OrderPackingLines(newOrderId,
                    [new OrderPackingLine(newOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())])
            ]);
        var deliveries = Substitute.For<IDeliveryRepository>();
        deliveries.GetExistingOrderIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { reservedOrderId });
        var routes = Substitute.For<IDeliveryRouteRepository>();
        routes.GetReservedVehicleIdsAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { reservedVehicle.Id });
        var vehicles = Substitute.For<IVehicleRepository>();
        vehicles.GetPageAsync(null, 10_000, true, hubId, Arg.Any<CancellationToken>())
            .Returns((new[] { reservedVehicle, freeVehicle }, null));
        var settings = Substitute.For<IVehicleCapacityPolicy>();
        var sessionVehicles = Substitute.For<IMarketSessionVehicleReader>();
        sessionVehicles.ReadAssignedVehicleIdsAsync(hubId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { freeVehicle.Id });
        settings.CapacityUtilizationPercent.Returns(90m);
        settings.BoxTareKg.Returns(1m);
        settings.MaxStopsPerVehicle.Returns(20);
        var sut = new RoutePlanningInputBuilder(
            hubs, restaurants, orders, packing, deliveries, routes, vehicles, sessionVehicles, settings);

        var result = await sut.BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Demands.Should().ContainSingle()
            .Which.OrderIds.Should().Equal(newOrderId);
        result.Value.Vehicles.Should().ContainSingle()
            .Which.Id.Should().Be(freeVehicle.Id);
        await packing.Received(1).GetLinesByOrdersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { newOrderId })),
            Arg.Any<CancellationToken>());
        await vehicles.Received(1).GetPageAsync(
            null, 10_000, true, hubId, Arg.Any<CancellationToken>());
    }
}
