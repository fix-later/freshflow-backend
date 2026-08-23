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
    public async Task BuildAsync_IncludesBatchedAndExcludesReservedOrdersAndVehiclesAsync()
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
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>(),
                hubId, Arg.Any<DateOnly>())
            .Returns([
                new OrderStatusLookupDto(reservedOrderId, "AtHub", restaurantId, hubId, 10.1m, 106.1m),
                new OrderStatusLookupDto(newOrderId, "Batched", restaurantId, hubId, 10.2m, 106.2m)
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
        await orders.Received(1).ListByRestaurantsAndStatusAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Is<IReadOnlyCollection<string>>(statuses =>
                statuses.SequenceEqual(new[] { "Batched", "PickedUp", "AtHub" })),
            Arg.Any<CancellationToken>(), hubId, Arg.Any<DateOnly>());
        await vehicles.Received(1).GetPageAsync(
            null, 10_000, true, hubId, Arg.Any<CancellationToken>());
    }

    // ── M7: per-order isolation ─────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_OneOrderMissingPackingCapacity_OtherRestaurantsStillPlanAsync()
    {
        var hubId = Guid.NewGuid();
        var goodRestaurantId = Guid.NewGuid();
        var badRestaurantId = Guid.NewGuid();
        var goodOrderId = Guid.NewGuid();
        var badOrderId = Guid.NewGuid();

        var sut = CreateSut(
            hubId,
            orders:
            [
                new OrderStatusLookupDto(goodOrderId, "AtHub", goodRestaurantId, hubId, 10.1m, 106.1m),
                new OrderStatusLookupDto(badOrderId, "AtHub", badRestaurantId, hubId, 10.2m, 106.2m)
            ],
            restaurantsById: new Dictionary<Guid, RestaurantCoordinateDto?>
            {
                [goodRestaurantId] = new RestaurantCoordinateDto(goodRestaurantId, "Good", 10.1m, 106.1m),
                [badRestaurantId] = new RestaurantCoordinateDto(badRestaurantId, "Bad", 10.2m, 106.2m)
            },
            packingLines:
            [
                new OrderPackingLines(goodOrderId,
                    [new OrderPackingLine(goodOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())]),
                new OrderPackingLines(badOrderId,
                    [new OrderPackingLine(badOrderId, Guid.NewGuid(), "Product", 10m, null, Guid.NewGuid())])
            ]);

        var result = await sut.BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Demands.Should().ContainSingle()
            .Which.OrderIds.Should().Equal(goodOrderId);
        result.Value.Excluded.Should().ContainSingle()
            .Which.Should().Match<FreshFlow.Logistics.Domain.ValueObjects.RoutePlanUnassigned>(u =>
                u.RestaurantId == badRestaurantId
                && u.OrderIds.SequenceEqual(new[] { badOrderId })
                && u.ExcludedForIncompleteData);
    }

    [Fact]
    public async Task BuildAsync_RestaurantWithMixOfGoodAndBadOrders_SplitsBetweenDemandsAndExcludedAsync()
    {
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var goodOrderId = Guid.NewGuid();
        var badOrderId = Guid.NewGuid();

        var sut = CreateSut(
            hubId,
            orders:
            [
                new OrderStatusLookupDto(goodOrderId, "AtHub", restaurantId, hubId, 10.1m, 106.1m),
                new OrderStatusLookupDto(badOrderId, "AtHub", restaurantId, hubId, 10.1m, 106.1m)
            ],
            restaurantsById: new Dictionary<Guid, RestaurantCoordinateDto?>
            {
                [restaurantId] = new RestaurantCoordinateDto(restaurantId, "Mixed", 10.1m, 106.1m)
            },
            packingLines:
            [
                new OrderPackingLines(goodOrderId,
                    [new OrderPackingLine(goodOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())]),
                new OrderPackingLines(badOrderId, [])
            ]);

        var result = await sut.BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Demands.Should().ContainSingle()
            .Which.OrderIds.Should().Equal(goodOrderId);
        result.Value.Excluded.Should().ContainSingle()
            .Which.OrderIds.Should().Equal(badOrderId);
    }

    [Fact]
    public async Task BuildAsync_OrderMissingDeliveryCoordinates_IsExcludedNotWholeBatchFailureAsync()
    {
        var hubId = Guid.NewGuid();
        var goodRestaurantId = Guid.NewGuid();
        var badRestaurantId = Guid.NewGuid();
        var goodOrderId = Guid.NewGuid();
        var badOrderId = Guid.NewGuid();

        var sut = CreateSut(
            hubId,
            orders:
            [
                new OrderStatusLookupDto(goodOrderId, "AtHub", goodRestaurantId, hubId, 10.1m, 106.1m),
                new OrderStatusLookupDto(badOrderId, "AtHub", badRestaurantId, hubId, null, null)
            ],
            restaurantsById: new Dictionary<Guid, RestaurantCoordinateDto?>
            {
                [goodRestaurantId] = new RestaurantCoordinateDto(goodRestaurantId, "Good", 10.1m, 106.1m),
                [badRestaurantId] = new RestaurantCoordinateDto(badRestaurantId, "Bad", 10.2m, 106.2m)
            },
            packingLines:
            [
                new OrderPackingLines(goodOrderId,
                    [new OrderPackingLine(goodOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())]),
                new OrderPackingLines(badOrderId,
                    [new OrderPackingLine(badOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())])
            ]);

        var result = await sut.BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Demands.Should().ContainSingle()
            .Which.RestaurantId.Should().Be(goodRestaurantId);
        result.Value.Excluded.Should().ContainSingle()
            .Which.Reason.Should().Contain("checkout delivery coordinates");
    }

    [Fact]
    public async Task BuildAsync_MissingRestaurantRow_IsExcludedNotWholeBatchFailureAsync()
    {
        var hubId = Guid.NewGuid();
        var goodRestaurantId = Guid.NewGuid();
        var missingRestaurantId = Guid.NewGuid();
        var goodOrderId = Guid.NewGuid();
        var orphanOrderId = Guid.NewGuid();

        var sut = CreateSut(
            hubId,
            orders:
            [
                new OrderStatusLookupDto(goodOrderId, "AtHub", goodRestaurantId, hubId, 10.1m, 106.1m),
                new OrderStatusLookupDto(orphanOrderId, "AtHub", missingRestaurantId, hubId, 10.2m, 106.2m)
            ],
            restaurantsById: new Dictionary<Guid, RestaurantCoordinateDto?>
            {
                [goodRestaurantId] = new RestaurantCoordinateDto(goodRestaurantId, "Good", 10.1m, 106.1m)
                // missingRestaurantId intentionally absent — row not found
            },
            packingLines:
            [
                new OrderPackingLines(goodOrderId,
                    [new OrderPackingLine(goodOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())]),
                new OrderPackingLines(orphanOrderId,
                    [new OrderPackingLine(orphanOrderId, Guid.NewGuid(), "Product", 10m, 5m, Guid.NewGuid())])
            ]);

        var result = await sut.BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Excluded.Should().ContainSingle()
            .Which.Should().Match<FreshFlow.Logistics.Domain.ValueObjects.RoutePlanUnassigned>(u =>
                u.RestaurantId == missingRestaurantId
                && u.Reason == "Restaurant record is unavailable.");
    }

    [Fact]
    public async Task BuildAsync_ExcludedVsIncludedOrder_ProducesDifferentInputRevisionAsync()
    {
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        RoutePlanningInputBuilder SutFor(decimal? capacityKg) => CreateSut(
            hubId,
            orders: [new OrderStatusLookupDto(orderId, "AtHub", restaurantId, hubId, 10.1m, 106.1m)],
            restaurantsById: new Dictionary<Guid, RestaurantCoordinateDto?>
            {
                [restaurantId] = new RestaurantCoordinateDto(restaurantId, "R", 10.1m, 106.1m)
            },
            packingLines:
            [
                new OrderPackingLines(orderId,
                    [new OrderPackingLine(orderId, Guid.NewGuid(), "Product", 10m, capacityKg, Guid.NewGuid())])
            ]);

        var included = await SutFor(5m).BuildAsync(hubId, new DateOnly(2026, 8, 10), default);
        var excluded = await SutFor(null).BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        included.IsSuccess.Should().BeTrue();
        excluded.IsSuccess.Should().BeTrue();
        included.Value.InputRevision.Should().NotBe(excluded.Value.InputRevision,
            "an order flipping between included and excluded must invalidate the cached plan revision");
    }

    [Fact]
    public async Task BuildAsync_HubWithNoCoordinates_StillHardFailsAsync()
    {
        var hubId = Guid.NewGuid();
        var sut = CreateSut(hubId, orders: [], restaurantsById: new Dictionary<Guid, RestaurantCoordinateDto?>(),
            packingLines: [], hubLatitude: null, hubLongitude: null);

        var result = await sut.BuildAsync(hubId, new DateOnly(2026, 8, 10), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MISSING_COORDINATES");
    }

    /// <summary>
    /// Wires a builder with sensible defaults (empty fleet, nothing reserved) so each M7 test
    /// only has to specify the orders/restaurants/packing that matter to it.
    /// </summary>
    private static RoutePlanningInputBuilder CreateSut(
        Guid hubId,
        IReadOnlyList<OrderStatusLookupDto> orders,
        IReadOnlyDictionary<Guid, RestaurantCoordinateDto?> restaurantsById,
        IReadOnlyList<OrderPackingLines> packingLines,
        decimal? hubLatitude = 10m,
        decimal? hubLongitude = 106m)
    {
        var hubs = Substitute.For<IHubCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, null, "Hub", hubLatitude, hubLongitude));

        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        restaurants.FindByRestaurantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => restaurantsById.GetValueOrDefault(ci.Arg<Guid>()));

        var orderReader = Substitute.For<IOrderStatusReader>();
        var restaurantIds = orders.Select(o => o.RestaurantId).Distinct().ToList();
        orderReader.ListRoutableRestaurantsAsync(
                Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(restaurantIds.Select(id => (id, 1)).ToList());
        orderReader.ListByRestaurantsAndStatusAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>(), Arg.Any<Guid?>(), Arg.Any<DateOnly?>())
            .Returns(orders.ToList());

        var packing = Substitute.For<IOrderPackingReader>();
        packing.GetLinesByOrdersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(packingLines.ToList());

        var deliveries = Substitute.For<IDeliveryRepository>();
        deliveries.GetExistingOrderIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        var routes = Substitute.For<IDeliveryRouteRepository>();
        routes.GetReservedVehicleIdsAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        var vehicles = Substitute.For<IVehicleRepository>();
        vehicles.GetPageAsync(null, 10_000, true, hubId, Arg.Any<CancellationToken>())
            .Returns((new List<Vehicle>(), (string?)null));

        var settings = Substitute.For<IVehicleCapacityPolicy>();
        settings.CapacityUtilizationPercent.Returns(90m);
        settings.BoxTareKg.Returns(1m);
        settings.MaxStopsPerVehicle.Returns(20);

        var sessionVehicles = Substitute.For<IMarketSessionVehicleReader>();
        sessionVehicles.ReadAssignedVehicleIdsAsync(hubId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        return new RoutePlanningInputBuilder(
            hubs, restaurants, orderReader, packing, deliveries, routes, vehicles, sessionVehicles, settings);
    }
}
