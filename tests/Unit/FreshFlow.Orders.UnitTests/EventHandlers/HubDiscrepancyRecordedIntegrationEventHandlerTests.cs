using System.Reflection;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyRecordedIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ValidDiscrepancy_RecordsQuantityAsOrderedMinusAffectedAndSavesAsync()
    {
        // AUDIT-2026-08-23 C4: the handler no longer refunds directly — it records the actual
        // quantity via Order.RecordActualQuantity, which raises OrderProcurementShortfallDomainEvent
        // (refund + invoice quantity move together from a single source of truth).
        var order = ConfirmedOrderWithItem(out var itemId); // ordered 5
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            itemId,
            3m,
            "MISSING",
            DateTime.UtcNow), default);

        order.Items.Single().ActualQuantity.Should().Be(2m); // 5 ordered - 3 affected
        _orders.Received(1).Track(order);
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_SecondDiscrepancyOnSameLine_MeasuresFromPreviousActualAsync()
    {
        // A second hub discrepancy on the same line must subtract from the previous actual, not
        // the ordered quantity — Order.RecordActualQuantity already guarantees this; this test
        // proves the handler feeds it the right baseline.
        var order = ConfirmedOrderWithItem(out var itemId); // ordered 5
        order.RecordActualQuantity(itemId, 4m); // first discrepancy already recorded: 5 -> 4
        order.ClearDomainEvents();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            itemId,
            1m,
            "DAMAGED",
            DateTime.UtcNow), default);

        order.Items.Single().ActualQuantity.Should().Be(3m); // 4 previous actual - 1 affected
        await _orders.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_RecordActualQuantityFails_LogsAndDoesNotSaveAsync()
    {
        // A draft order can't be adjusted (ORDER_CANNOT_ADJUST) — the failure must be logged,
        // not thrown, and nothing gets persisted.
        var order = DraftOrderWithItem(out var itemId);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            itemId,
            1m,
            "DAMAGED",
            DateTime.UtcNow), default);

        _orders.DidNotReceive().Track(Arg.Any<Order>());
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderNotFound_DoesNotSaveAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.FindByIdAsync(orderId, default).Returns((Order?)null);
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            orderId,
            Guid.NewGuid(),
            1m,
            "MISSING",
            DateTime.UtcNow), default);

        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderItemNotFound_DoesNotSaveAsync()
    {
        var order = ConfirmedOrderWithItem(out _);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            Guid.NewGuid(),
            1m,
            "MISSING",
            DateTime.UtcNow), default);

        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_DoesNotDependOnPaymentGateway()
    {
        var parameterNames = typeof(HubDiscrepancyRecordedIntegrationEventHandler)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name);

        parameterNames.Should().NotContain(name =>
            name.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gateway", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Credit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Publisher", StringComparison.OrdinalIgnoreCase));
    }

    private HubDiscrepancyRecordedIntegrationEventHandler CreateSut() =>
        new(
            _orders,
            Substitute.For<ILogger<HubDiscrepancyRecordedIntegrationEventHandler>>());

    private static Order ConfirmedOrderWithItem(out Guid itemId)
    {
        var order = DraftOrderWithItem(out itemId);
        order.Confirm();
        return order;
    }

    private static Order DraftOrderWithItem(out Guid itemId)
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        itemId = order.Items.Single().Id;
        return order;
    }
}
