using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using MediatR;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateDeliveryStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_Arrived_PendingDeliveryMarksArrivedAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateInProgressRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var (routes, deliveries, publisher, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(delivery.Id, driverId, "ARRIVED", null),
            default);

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(Delivery.StatusArrived);
        deliveries.SaveChangesCount.Should().Be(1);
        await publisher.DidNotReceive().Publish(
            Arg.Any<DeliveryCompletedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
        routes.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ArrivedAgain_ReturnsConflictAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateInProgressRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        delivery.MarkArrived();
        var (_, deliveries, _, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(delivery.Id, driverId, "ARRIVED", null),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DELIVERY_STATUS_INVALID");
        deliveries.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Delivered_ArrivedDeliveryPublishesCompletionAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateInProgressRoute(driverId);
        var orderId = Guid.NewGuid();
        var delivery = Delivery.Create(route.Id, orderId, 1);
        delivery.MarkArrived();
        var (_, deliveries, publisher, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(delivery.Id, driverId, "DELIVERED", null),
            default);

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(Delivery.StatusDelivered);
        deliveries.SaveChangesCount.Should().Be(1);
        await publisher.Received(1).Publish(
            Arg.Is<DeliveryCompletedIntegrationEvent>(evt =>
                evt.OrderId == orderId &&
                evt.RouteId == route.Id &&
                evt.ActualArrivalAt == delivery.ActualArrival),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Failed_PendingDeliverySavesReasonWithoutPublishingAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateInProgressRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var (_, deliveries, publisher, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(delivery.Id, driverId, "FAILED", "customer unavailable"),
            default);

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(Delivery.StatusFailed);
        delivery.FailureReason.Should().Be("customer unavailable");
        deliveries.SaveChangesCount.Should().Be(1);
        await publisher.DidNotReceive().Publish(
            Arg.Any<DeliveryCompletedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeliveryAssignedToAnotherDriver_ReturnsForbiddenAsync()
    {
        var route = CreateInProgressRoute(Guid.NewGuid());
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var (_, deliveries, publisher, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(delivery.Id, Guid.NewGuid(), "ARRIVED", null),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FORBIDDEN");
        deliveries.SaveChangesCount.Should().Be(0);
        deliveries.GetByRouteIdsCount.Should().Be(0);
        await publisher.DidNotReceive().Publish(
            Arg.Any<DeliveryCompletedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeliveryNotFound_Returns404Async()
    {
        var sut = new UpdateDeliveryStatusCommandHandler(
            new InMemoryDeliveryRepository(),
            new InMemoryDeliveryRouteRepository(),
            Substitute.For<IPublisher>());

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(Guid.NewGuid(), Guid.NewGuid(), "ARRIVED", null),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DELIVERY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OneOfTwoDeliveriesTerminal_KeepsRouteInProgressAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateInProgressRoute(driverId);
        var first = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var second = Delivery.Create(route.Id, Guid.NewGuid(), 2);
        first.MarkArrived();
        var (_, _, _, sut) = await CreateSutAsync(route, [first, second]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(first.Id, driverId, "DELIVERED", null),
            default);

        result.IsSuccess.Should().BeTrue();
        route.Status.Should().Be(RouteStatus.in_progress);
    }

    [Fact]
    public async Task Handle_AllDeliveriesTerminal_CompletesRouteAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateInProgressRoute(driverId);
        var first = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var second = Delivery.Create(route.Id, Guid.NewGuid(), 2);
        first.MarkFailed("not available");
        second.MarkArrived();
        var (_, _, _, sut) = await CreateSutAsync(route, [first, second]);

        var result = await sut.Handle(
            new UpdateDeliveryStatusCommand(second.Id, driverId, "DELIVERED", null),
            default);

        result.IsSuccess.Should().BeTrue();
        route.Status.Should().Be(RouteStatus.completed);
    }

    private static async Task<(
        InMemoryDeliveryRouteRepository Routes,
        InMemoryDeliveryRepository Deliveries,
        IPublisher Publisher,
        UpdateDeliveryStatusCommandHandler Sut)> CreateSutAsync(
            DeliveryRoute route,
            IReadOnlyList<Delivery> routeDeliveries)
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var publisher = Substitute.For<IPublisher>();
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync(routeDeliveries, default);
        return (routes, deliveries, publisher, new UpdateDeliveryStatusCommandHandler(deliveries, routes, publisher));
    }

    private static DeliveryRoute CreateInProgressRoute(Guid driverId)
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
        route.Start();
        return route;
    }
}
