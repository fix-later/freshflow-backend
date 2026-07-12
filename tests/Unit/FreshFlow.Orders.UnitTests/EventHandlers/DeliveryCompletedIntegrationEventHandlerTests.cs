using System.Reflection;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class DeliveryCompletedIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task Handle_OrderDelivering_AdvancesToDeliveredAndSavesAsync()
    {
        var order = CreateOrder(OrderStatus.Delivering);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        await sut.Handle(
            new DeliveryCompletedIntegrationEvent(order.Id, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
            default);

        order.Status.Should().Be(OrderStatus.Delivered);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_OrderNotFound_SkipsWithoutThrowingAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.FindByIdAsync(orderId, default).Returns((Order?)null);
        var sut = CreateSut();

        var act = () => sut.Handle(
            new DeliveryCompletedIntegrationEvent(orderId, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
            default);

        await act.Should().NotThrowAsync();
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderAlreadyDelivered_SkipsWithoutThrowingAsync()
    {
        var order = CreateOrder(OrderStatus.Delivered);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        var act = () => sut.Handle(
            new DeliveryCompletedIntegrationEvent(order.Id, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
            default);

        await act.Should().NotThrowAsync();
        order.Status.Should().Be(OrderStatus.Delivered);
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private DeliveryCompletedIntegrationEventHandler CreateSut() =>
        new(_orders, Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

    private static Order CreateOrder(OrderStatus status)
    {
        var order = new Order(Guid.NewGuid(), scheduledFor: null, notes: null);
        SetStatus(order, status);
        return order;
    }

    private static void SetStatus(Order order, OrderStatus status)
    {
        var field = typeof(Order).GetField(
            $"<{nameof(Order.Status)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(order, status);
    }
}
