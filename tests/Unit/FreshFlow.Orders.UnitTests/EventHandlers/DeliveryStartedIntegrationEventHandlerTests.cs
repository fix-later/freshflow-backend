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
public sealed class DeliveryStartedIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task Handle_OrderAtHub_AdvancesToDeliveringAndSavesAsync()
    {
        var order = CreateOrder(OrderStatus.AtHub);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        await sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [order.Id], DateTime.UtcNow),
            default);

        order.Status.Should().Be(OrderStatus.Delivering);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_OrderNotAtHub_SkipsWithoutThrowingAsync()
    {
        var order = CreateOrder(OrderStatus.Confirmed);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        var act = () => sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [order.Id], DateTime.UtcNow),
            default);

        await act.Should().NotThrowAsync();
        order.Status.Should().Be(OrderStatus.Confirmed);
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderNotFound_SkipsWithoutThrowingAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.FindByIdAsync(orderId, default).Returns((Order?)null);
        var sut = CreateSut();

        var act = () => sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [orderId], DateTime.UtcNow),
            default);

        await act.Should().NotThrowAsync();
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ThreeOrders_AdvancesAndSavesEachAsync()
    {
        var first = CreateOrder(OrderStatus.AtHub);
        var second = CreateOrder(OrderStatus.AtHub);
        var third = CreateOrder(OrderStatus.AtHub);
        _orders.FindByIdAsync(first.Id, default).Returns(first);
        _orders.FindByIdAsync(second.Id, default).Returns(second);
        _orders.FindByIdAsync(third.Id, default).Returns(third);
        var sut = CreateSut();

        await sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [first.Id, second.Id, third.Id], DateTime.UtcNow),
            default);

        first.Status.Should().Be(OrderStatus.Delivering);
        second.Status.Should().Be(OrderStatus.Delivering);
        third.Status.Should().Be(OrderStatus.Delivering);
        await _orders.Received(3).SaveChangesAsync(default);
    }

    private DeliveryStartedIntegrationEventHandler CreateSut() =>
        new(_orders, Substitute.For<ILogger<DeliveryStartedIntegrationEventHandler>>());

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
