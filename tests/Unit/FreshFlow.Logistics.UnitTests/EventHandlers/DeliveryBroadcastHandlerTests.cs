using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.EventHandlers;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class DeliveryBroadcastHandlerTests
{
    private readonly InMemoryOrderStatusReader _orders = new();
    private readonly IDeliveryBroadcastService _broadcast = Substitute.For<IDeliveryBroadcastService>();

    [Fact]
    public async Task DeliveryStarted_WithResolvedOrder_BroadcastsStartedUpdateAsync()
    {
        var routeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        _orders.Add(orderId, "AtHub", restaurantId);
        var sut = new DeliveryStartedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStartedBroadcastHandler>.Instance);

        await sut.Handle(new DeliveryStartedIntegrationEvent(routeId, [orderId], occurredAt), default);

        await _broadcast.Received(1).BroadcastDeliveryStartedAsync(
            restaurantId,
            Arg.Is<DeliveryRealtimeUpdate>(update =>
                update.OrderId == orderId &&
                update.RouteId == routeId &&
                update.DeliveryId == null &&
                update.Status == "started" &&
                update.OccurredAt == occurredAt),
            default);
    }

    [Fact]
    public async Task DeliveryStarted_MissingOrder_SkipsBroadcastAsync()
    {
        var sut = new DeliveryStartedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStartedBroadcastHandler>.Instance);

        await sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [Guid.NewGuid()], DateTime.UtcNow),
            default);

        await _broadcast.DidNotReceive().BroadcastDeliveryStartedAsync(
            Arg.Any<Guid>(),
            Arg.Any<DeliveryRealtimeUpdate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryStarted_BroadcastThrows_DoesNotThrowAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.Add(orderId, "AtHub", Guid.NewGuid());
        _broadcast.BroadcastDeliveryStartedAsync(
                Arg.Any<Guid>(),
                Arg.Any<DeliveryRealtimeUpdate>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("signalr down")));
        var sut = new DeliveryStartedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStartedBroadcastHandler>.Instance);

        var act = async () => await sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [orderId], DateTime.UtcNow),
            default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeliveryStarted_OneOrderThrows_StillBroadcastsRemainingOrdersAsync()
    {
        var routeId = Guid.NewGuid();
        var failingOrderId = Guid.NewGuid();
        var okOrderId = Guid.NewGuid();
        var okRestaurantId = Guid.NewGuid();
        _orders.Add(failingOrderId, "AtHub", Guid.NewGuid());
        _orders.Add(okOrderId, "AtHub", okRestaurantId);
        _broadcast.BroadcastDeliveryStartedAsync(
                Arg.Any<Guid>(),
                Arg.Is<DeliveryRealtimeUpdate>(update => update.OrderId == failingOrderId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("signalr down")));
        var sut = new DeliveryStartedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStartedBroadcastHandler>.Instance);

        await sut.Handle(
            new DeliveryStartedIntegrationEvent(routeId, [failingOrderId, okOrderId], DateTime.UtcNow),
            default);

        await _broadcast.Received(1).BroadcastDeliveryStartedAsync(
            okRestaurantId,
            Arg.Is<DeliveryRealtimeUpdate>(update => update.OrderId == okOrderId),
            default);
    }

    [Fact]
    public async Task DeliveryStopUpdated_WithResolvedOrder_BroadcastsStopUpdateAsync()
    {
        var routeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        _orders.Add(orderId, "Delivering", restaurantId);
        var sut = new DeliveryStopUpdatedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStopUpdatedBroadcastHandler>.Instance);

        await sut.Handle(new DeliveryStopUpdatedIntegrationEvent(
            orderId,
            routeId,
            deliveryId,
            Delivery.StatusDelivered,
            occurredAt), default);

        await _broadcast.Received(1).BroadcastDeliveryStopUpdatedAsync(
            restaurantId,
            Arg.Is<DeliveryRealtimeUpdate>(update =>
                update.OrderId == orderId &&
                update.RouteId == routeId &&
                update.DeliveryId == deliveryId &&
                update.Status == Delivery.StatusDelivered &&
                update.OccurredAt == occurredAt),
            default);
    }

    [Fact]
    public async Task DeliveryStopUpdated_MissingOrder_SkipsBroadcastAsync()
    {
        var sut = new DeliveryStopUpdatedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStopUpdatedBroadcastHandler>.Instance);

        await sut.Handle(new DeliveryStopUpdatedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Delivery.StatusFailed,
            DateTime.UtcNow), default);

        await _broadcast.DidNotReceive().BroadcastDeliveryStopUpdatedAsync(
            Arg.Any<Guid>(),
            Arg.Any<DeliveryRealtimeUpdate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryStopUpdated_BroadcastThrows_DoesNotThrowAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.Add(orderId, "Delivering", Guid.NewGuid());
        _broadcast.BroadcastDeliveryStopUpdatedAsync(
                Arg.Any<Guid>(),
                Arg.Any<DeliveryRealtimeUpdate>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("signalr down")));
        var sut = new DeliveryStopUpdatedBroadcastHandler(
            _orders,
            _broadcast,
            NullLogger<DeliveryStopUpdatedBroadcastHandler>.Instance);

        var act = async () => await sut.Handle(new DeliveryStopUpdatedIntegrationEvent(
            orderId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Delivery.StatusFailed,
            DateTime.UtcNow), default);

        await act.Should().NotThrowAsync();
    }
}
