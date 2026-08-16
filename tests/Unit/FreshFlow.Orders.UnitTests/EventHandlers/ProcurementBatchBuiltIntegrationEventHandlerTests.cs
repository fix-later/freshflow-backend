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
public sealed class ProcurementBatchBuiltIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task Handle_ConfirmedOrder_AdvancesToBatchedAsync()
    {
        var order = CreateConfirmedOrder();
        _orders.FindByIdAsync(order.Id, default).Returns(order);

        await CreateSut().Handle(CreateEvent([order.Id]), default);

        order.Status.Should().Be(OrderStatus.Batched);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_DuplicateDelivery_IsIdempotentAsync()
    {
        var order = CreateConfirmedOrder();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();
        var notification = CreateEvent([order.Id, order.Id]);

        await sut.Handle(notification, default);
        var secondDelivery = () => sut.Handle(notification, default);

        await secondDelivery.Should().NotThrowAsync();
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_MissingOrder_LogsAndSkipsAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.FindByIdAsync(orderId, default).Returns((Order?)null);

        var act = () => CreateSut().Handle(CreateEvent([orderId]), default);

        await act.Should().NotThrowAsync();
        await _orders.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    private ProcurementBatchBuiltIntegrationEventHandler CreateSut() =>
        new(
            _orders,
            Substitute.For<ILogger<ProcurementBatchBuiltIntegrationEventHandler>>());

    private static ProcurementBatchBuiltIntegrationEvent CreateEvent(
        IReadOnlyList<Guid> orderIds) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 15),
            orderIds);

    private static Order CreateConfirmedOrder()
    {
        var order = new Order(Guid.NewGuid(), DateTime.UtcNow, null);
        typeof(Order)
            .GetField(
                $"<{nameof(Order.Status)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(order, OrderStatus.Confirmed);
        return order;
    }
}
