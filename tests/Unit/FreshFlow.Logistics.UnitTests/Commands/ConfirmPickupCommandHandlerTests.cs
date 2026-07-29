using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.ConfirmPickup;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ConfirmPickupCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignedRouteWithAtHubOrders_CreatesPendingDeliveriesAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var route = AssignedRoute(driverId, restaurantId);
        var orderIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await routes.AddAsync(route, default);
        foreach (var orderId in orderIds)
            orders.Add(orderId, "AtHub", restaurantId);
        var sut = new ConfirmPickupCommandHandler(routes, deliveries, orders);

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, orderIds), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RouteId.Should().Be(route.Id);
        result.Value.DeliveryIds.Should().BeEquivalentTo(deliveries.Deliveries.Select(d => d.Id));
        deliveries.Deliveries.Should().HaveCount(2);
        deliveries.Deliveries.Select(d => d.Status).Should().OnlyContain(s => s == Delivery.StatusPending);
        deliveries.Deliveries.Select(d => d.SequenceNumber).Should().Equal(1, 2);
        deliveries.Deliveries.Select(d => d.OrderId).Should().Equal(orderIds);
        deliveries.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MissingOneAtHubOrder_ReturnsPickupOrdersIncompleteAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var route = AssignedRoute(driverId, restaurantId);
        var firstOrderId = Guid.NewGuid();
        await routes.AddAsync(route, default);
        orders.Add(firstOrderId, "AtHub", restaurantId);
        orders.Add(Guid.NewGuid(), "AtHub", restaurantId);
        var sut = new ConfirmPickupCommandHandler(routes, deliveries, orders);

        var result = await sut.Handle(
            new ConfirmPickupCommand(route.Id, driverId, [firstOrderId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PICKUP_ORDERS_INCOMPLETE");
        deliveries.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrdersRequestedOutOfStopOrder_AssignsSequenceByRouteStopOrderAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var restaurantAId = Guid.NewGuid();
        var restaurantBId = Guid.NewGuid();
        var route = ReviewedRouteWithTwoRestaurants(restaurantBId, restaurantAId);
        route.Assign(Guid.NewGuid(), driverId);
        var orderAtRestaurantA = Guid.NewGuid();
        var orderAtRestaurantB = Guid.NewGuid();
        await routes.AddAsync(route, default);
        orders.Add(orderAtRestaurantA, "AtHub", restaurantAId);
        orders.Add(orderAtRestaurantB, "AtHub", restaurantBId);
        var sut = new ConfirmPickupCommandHandler(routes, deliveries, orders);

        var result = await sut.Handle(
            new ConfirmPickupCommand(route.Id, driverId, [orderAtRestaurantA, orderAtRestaurantB]),
            default);

        result.IsSuccess.Should().BeTrue();
        var byOrder = deliveries.Deliveries.ToDictionary(d => d.OrderId, d => d.SequenceNumber);
        byOrder[orderAtRestaurantB].Should().Be(1);
        byOrder[orderAtRestaurantA].Should().Be(2);
    }

    [Fact]
    public async Task Handle_ConcurrentSaveHitsUniqueViolation_ReturnsConflictAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository { TrySaveChangesResult = false };
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var route = AssignedRoute(driverId, restaurantId);
        var orderId = Guid.NewGuid();
        await routes.AddAsync(route, default);
        orders.Add(orderId, "AtHub", restaurantId);
        var sut = new ConfirmPickupCommandHandler(routes, deliveries, orders);

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, [orderId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_RouteMissing_ReturnsNotFoundAsync()
    {
        var sut = new ConfirmPickupCommandHandler(
            new InMemoryDeliveryRouteRepository(),
            new InMemoryDeliveryRepository(),
            new InMemoryOrderStatusReader());
        var routeId = Guid.NewGuid();

        var result = await sut.Handle(new ConfirmPickupCommand(routeId, Guid.NewGuid(), [Guid.NewGuid()]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RouteNotAssigned_ReturnsConflictAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var driverId = Guid.NewGuid();
        var route = AssignedRoute(driverId, Guid.NewGuid());
        route.Start();
        await routes.AddAsync(route, default);
        var sut = new ConfirmPickupCommandHandler(
            routes,
            new InMemoryDeliveryRepository(),
            new InMemoryOrderStatusReader());

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, [Guid.NewGuid()]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_NOT_ASSIGNED");
    }

    [Fact]
    public async Task Handle_RouteAssignedToDifferentDriver_ReturnsForbiddenAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var route = AssignedRoute(Guid.NewGuid(), Guid.NewGuid());
        await routes.AddAsync(route, default);
        var sut = new ConfirmPickupCommandHandler(
            routes,
            new InMemoryDeliveryRepository(),
            new InMemoryOrderStatusReader());

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, Guid.NewGuid(), [Guid.NewGuid()]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var driverId = Guid.NewGuid();
        var route = AssignedRoute(driverId, Guid.NewGuid());
        await routes.AddAsync(route, default);
        var sut = new ConfirmPickupCommandHandler(
            routes,
            new InMemoryDeliveryRepository(),
            new InMemoryOrderStatusReader());
        var orderId = Guid.NewGuid();

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, [orderId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PICKUP_ORDERS_INCOMPLETE");
    }

    [Fact]
    public async Task Handle_OrderNotAtHub_ReturnsValidationAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var route = AssignedRoute(driverId, restaurantId);
        var orderId = Guid.NewGuid();
        await routes.AddAsync(route, default);
        orders.Add(orderId, "Confirmed", restaurantId);
        var sut = new ConfirmPickupCommandHandler(routes, new InMemoryDeliveryRepository(), orders);

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, [orderId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PICKUP_ORDERS_INCOMPLETE");
    }

    [Fact]
    public async Task Handle_OrderRestaurantNotOnRoute_ReturnsValidationWithoutSavingAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var route = AssignedRoute(driverId, Guid.NewGuid());
        var orderId = Guid.NewGuid();
        await routes.AddAsync(route, default);
        orders.Add(orderId, "AtHub", Guid.NewGuid());
        var sut = new ConfirmPickupCommandHandler(routes, deliveries, orders);

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, [orderId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PICKUP_ORDERS_INCOMPLETE");
        deliveries.SaveChangesCount.Should().Be(0);
        deliveries.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrderAlreadyHasDelivery_ReturnsConflictAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var orders = new InMemoryOrderStatusReader();
        var driverId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var route = AssignedRoute(driverId, restaurantId);
        var orderId = Guid.NewGuid();
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync([Delivery.Create(Guid.NewGuid(), orderId, 1)], default);
        orders.Add(orderId, "AtHub", restaurantId);
        var sut = new ConfirmPickupCommandHandler(routes, deliveries, orders);

        var result = await sut.Handle(new ConfirmPickupCommand(route.Id, driverId, [orderId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ALREADY_EXISTS");
        deliveries.SaveChangesCount.Should().Be(0);
    }

    private static DeliveryRoute AssignedRoute(Guid driverId, Guid restaurantId)
    {
        var route = ReviewedRoute(restaurantId);
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }

    private static DeliveryRoute ReviewedRouteWithTwoRestaurants(Guid firstRestaurantId, Guid secondRestaurantId)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 11),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, firstRestaurantId, "Restaurant A", 10.2m, 106.2m, null, null),
                new RouteStop(2, StopEntityType.restaurant, secondRestaurantId, "Restaurant B", 10.3m, 106.3m, null, null)
            ],
            null);

        route.Select();
        route.ApplyOptimization(route.Stops, 12m, 30, 100m, OptimizationCriteria.cost);
        route.MarkReviewed();
        return route;
    }

    private static DeliveryRoute ReviewedRoute(Guid? restaurantId = null)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 11),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, restaurantId ?? Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);

        route.Select();
        route.ApplyOptimization(route.Stops, 12m, 30, 100m, OptimizationCriteria.cost);
        route.MarkReviewed();
        return route;
    }
}
