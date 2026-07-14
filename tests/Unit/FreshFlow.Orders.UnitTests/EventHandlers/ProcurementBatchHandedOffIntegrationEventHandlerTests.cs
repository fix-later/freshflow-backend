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
public sealed class ProcurementBatchHandedOffIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task Handle_BatchedOrder_AdvancesThroughBothHopsAsync()
    {
        var order = CreateOrder(OrderStatus.Batched);
        _orders.FindByIdAsync(order.Id, default).Returns(order);

        await CreateSut().Handle(CreateEvent([order.Id, order.Id]), default);

        order.Status.Should().Be(OrderStatus.AtHub);
        await _orders.Received(1).FindByIdAsync(order.Id, default);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_PickedUpOrder_AdvancesToAtHubAsync()
    {
        var order = CreateOrder(OrderStatus.PickedUp);
        _orders.FindByIdAsync(order.Id, default).Returns(order);

        await CreateSut().Handle(CreateEvent([order.Id]), default);

        order.Status.Should().Be(OrderStatus.AtHub);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_AtHubOrder_IsNoOpAsync()
    {
        var order = CreateOrder(OrderStatus.AtHub);
        _orders.FindByIdAsync(order.Id, default).Returns(order);

        var act = () => CreateSut().Handle(CreateEvent([order.Id]), default);

        await act.Should().NotThrowAsync();
        order.Status.Should().Be(OrderStatus.AtHub);
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

    [Fact]
    public async Task Handle_OneOrderFails_ContinuesWithRemainingOrdersAsync()
    {
        var failingOrderId = Guid.NewGuid();
        var succeedingOrder = CreateOrder(OrderStatus.Batched);
        _orders.FindByIdAsync(failingOrderId, default)
            .Returns(Task.FromException<Order?>(new InvalidOperationException("boom")));
        _orders.FindByIdAsync(succeedingOrder.Id, default)
            .Returns(succeedingOrder);

        var act = () => CreateSut().Handle(
            CreateEvent([failingOrderId, succeedingOrder.Id]),
            default);

        await act.Should().NotThrowAsync();
        succeedingOrder.Status.Should().Be(OrderStatus.AtHub);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    private ProcurementBatchHandedOffIntegrationEventHandler CreateSut() =>
        new(
            _orders,
            Substitute.For<ILogger<ProcurementBatchHandedOffIntegrationEventHandler>>());

    private static ProcurementBatchHandedOffIntegrationEvent CreateEvent(
        IReadOnlyList<Guid> orderIds) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc),
            orderIds);

    private static Order CreateOrder(OrderStatus status)
    {
        var order = new Order(Guid.NewGuid(), DateTime.UtcNow, null);
        typeof(Order)
            .GetField(
                $"<{nameof(Order.Status)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(order, status);
        order.ClearDomainEvents();
        return order;
    }
}
