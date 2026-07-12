using System.Reflection;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Commands.StartRoute;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using MediatR;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class StartRouteCommandHandlerTests
{
    [Fact]
    public async Task Handle_HappyPath_StartsRouteAndPublishesSingleEventAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateAssignedRoute(driverId);
        var orderIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var publisher = Substitute.For<IPublisher>();
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync(
            [
                Delivery.Create(route.Id, orderIds[0], 1),
                Delivery.Create(route.Id, orderIds[1], 2)
            ],
            default);
        var sut = new StartRouteCommandHandler(
            routes,
            deliveries,
            new InMemoryHubDiscrepancyStatusReader(),
            publisher);

        var result = await sut.Handle(new StartRouteCommand(route.Id, driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RouteId.Should().Be(route.Id);
        result.Value.Status.Should().Be(RouteStatus.in_progress.ToString());
        result.Value.StartedOrderCount.Should().Be(2);
        route.Status.Should().Be(RouteStatus.in_progress);
        routes.SaveChangesCount.Should().Be(1);
        await publisher.Received(1).Publish(
            Arg.Is<DeliveryStartedIntegrationEvent>(evt =>
                evt.RouteId == route.Id &&
                evt.OrderIds.SequenceEqual(orderIds)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RouteNotFound_Returns404Async()
    {
        var sut = new StartRouteCommandHandler(
            new InMemoryDeliveryRouteRepository(),
            new InMemoryDeliveryRepository(),
            new InMemoryHubDiscrepancyStatusReader(),
            Substitute.For<IPublisher>());

        var result = await sut.Handle(new StartRouteCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RouteAssignedToAnotherDriver_ReturnsForbiddenAsync()
    {
        var route = CreateAssignedRoute(Guid.NewGuid());
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(route, default);
        var sut = new StartRouteCommandHandler(
            routes,
            new InMemoryDeliveryRepository(),
            new InMemoryHubDiscrepancyStatusReader(),
            Substitute.For<IPublisher>());

        var result = await sut.Handle(new StartRouteCommand(route.Id, Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_RouteNotAssigned_ReturnsConflictAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateAssignedRoute(driverId);
        SetRouteStatus(route, RouteStatus.reviewed);
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(route, default);
        var sut = new StartRouteCommandHandler(
            routes,
            new InMemoryDeliveryRepository(),
            new InMemoryHubDiscrepancyStatusReader(),
            Substitute.For<IPublisher>());

        var result = await sut.Handle(new StartRouteCommand(route.Id, driverId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ROUTE_NOT_STARTABLE");
    }

    [Fact]
    public async Task Handle_RouteHasNoDeliveries_ReturnsConflictAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateAssignedRoute(driverId);
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(route, default);
        var sut = new StartRouteCommandHandler(
            routes,
            new InMemoryDeliveryRepository(),
            new InMemoryHubDiscrepancyStatusReader(),
            Substitute.For<IPublisher>());

        var result = await sut.Handle(new StartRouteCommand(route.Id, driverId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ROUTE_HAS_NO_DELIVERIES");
    }

    [Fact]
    public async Task Handle_OpenDiscrepancy_ReturnsConflictWithoutStartingOrPublishingAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateAssignedRoute(driverId);
        var blockedOrderId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var discrepancies = new InMemoryHubDiscrepancyStatusReader();
        var publisher = Substitute.For<IPublisher>();
        discrepancies.AddOpenDiscrepancy(blockedOrderId);
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync([Delivery.Create(route.Id, blockedOrderId, 1)], default);
        var sut = new StartRouteCommandHandler(routes, deliveries, discrepancies, publisher);

        var result = await sut.Handle(new StartRouteCommand(route.Id, driverId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PENDING_HUB_DISCREPANCY");
        route.Status.Should().Be(RouteStatus.assigned);
        routes.SaveChangesCount.Should().Be(0);
        await publisher.DidNotReceive().Publish(
            Arg.Any<DeliveryStartedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    private static DeliveryRoute CreateAssignedRoute(Guid driverId)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 11),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
        route.ApplyOptimization(route.Stops, 12.3m, 30, 50000m, OptimizationCriteria.distance);
        route.Select();
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }

    private static void SetRouteStatus(DeliveryRoute route, RouteStatus status)
    {
        var field = typeof(DeliveryRoute).GetField(
            "<Status>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(route, status);
    }
}
