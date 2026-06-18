using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class OrderStatusChangedDomainEventHandlerTests
{
    private readonly IOrderBroadcastService _broadcastService = Substitute.For<IOrderBroadcastService>();
    private readonly ILogger<OrderStatusChangedDomainEventHandler> _logger =
        Substitute.For<ILogger<OrderStatusChangedDomainEventHandler>>();
    private readonly OrderStatusChangedDomainEventHandler _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly DateTime OccurredAt = new(2026, 6, 18, 7, 30, 0, DateTimeKind.Utc);

    public OrderStatusChangedDomainEventHandlerTests()
    {
        _sut = new OrderStatusChangedDomainEventHandler(_broadcastService, _logger);
    }

    [Fact]
    public async Task Handle_ValidEvent_CallsBroadcastServiceOnceAsync()
    {
        var notification = NewEvent();

        await _sut.Handle(notification, default);

        await _broadcastService.Received(1).BroadcastStatusChangedAsync(
            Arg.Any<OrderStatusChangedBroadcastDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_MapsPayloadAsync()
    {
        var notification = NewEvent(
            previousStatus: OrderStatus.AtHub,
            newStatus: OrderStatus.Delivering);

        await _sut.Handle(notification, default);

        await _broadcastService.Received(1).BroadcastStatusChangedAsync(
            Arg.Is<OrderStatusChangedBroadcastDto>(dto =>
                dto.OrderId == OrderId
                && dto.RestaurantId == RestaurantId
                && dto.PreviousStatus == "at_hub"
                && dto.NewStatus == "delivering"
                && dto.ChangedAt == OccurredAt
                && dto.EstimatedDeliveryAt == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenAsync()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        await _sut.Handle(NewEvent(), ct);

        await _broadcastService.Received(1).BroadcastStatusChangedAsync(
            Arg.Any<OrderStatusChangedBroadcastDto>(),
            ct);
    }

    [Fact]
    public async Task Handle_BroadcastThrows_DoesNotPropagateExceptionAsync()
    {
        _broadcastService
            .BroadcastStatusChangedAsync(Arg.Any<OrderStatusChangedBroadcastDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var act = async () => await _sut.Handle(NewEvent(), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_BroadcastCanceled_PropagatesOperationCanceledExceptionAsync()
    {
        _broadcastService
            .BroadcastStatusChangedAsync(Arg.Any<OrderStatusChangedBroadcastDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await _sut.Handle(NewEvent(), default);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static OrderStatusChangedDomainEvent NewEvent(
        OrderStatus previousStatus = OrderStatus.Confirmed,
        OrderStatus newStatus = OrderStatus.Batched) =>
        new(OrderId, RestaurantId, previousStatus, newStatus, OccurredAt);
}
