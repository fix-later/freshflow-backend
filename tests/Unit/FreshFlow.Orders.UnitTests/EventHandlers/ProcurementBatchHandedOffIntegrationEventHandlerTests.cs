using System.Reflection;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchHandedOffIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();

    public ProcurementBatchHandedOffIntegrationEventHandlerTests()
    {
        _orders.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<FreshFlow.SharedKernel.Application.Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<FreshFlow.SharedKernel.Application.Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _orders.ConsumeStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _orders.ReleaseStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task Handle_FullPurchase_RecordsActualsAndAdvancesAllOrdersAsync()
    {
        var productId = Guid.NewGuid();
        var first = CreateOrder(OrderStatus.Batched, productId, 1);
        var second = CreateOrder(OrderStatus.Batched, productId, 3);
        ConfigureOrders(first, second);

        await CreateSut().Handle(
            CreateEvent(
                [second.Id, first.Id],
                [new ProcurementPurchaseActual(productId, 4, 12_000m)]),
            default);

        first.Status.Should().Be(OrderStatus.AtHub);
        second.Status.Should().Be(OrderStatus.AtHub);
        first.Items.Single().ActualQuantity.Should().Be(1m);
        second.Items.Single().ActualQuantity.Should().Be(3m);
        first.Items.Single().ActualUnitPrice.Should().Be(12_000m);
        second.Items.Single().ActualUnitPrice.Should().Be(12_000m);
        await _orders.Received(1).ConsumeStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values =>
                values.Count == 1 && values[0] == new StockReservation(productId, 4)),
            default);
        await _orders.Received(1).ReleaseStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values => values.Count == 0),
            default);
    }

    [Fact]
    public async Task Handle_Shortage_AllocatesProRataAndFinalizesReservationAsync()
    {
        var productId = Guid.NewGuid();
        var first = CreateOrder(OrderStatus.Batched, productId, 1);
        var second = CreateOrder(OrderStatus.Batched, productId, 3);
        ConfigureOrders(second, first);

        await CreateSut().Handle(
            CreateEvent(
                [second.Id, first.Id],
                [new ProcurementPurchaseActual(productId, 2, 11_000m)]),
            default);

        first.Items.Single().ActualQuantity.Should().Be(0.5m);
        second.Items.Single().ActualQuantity.Should().Be(1.5m);
        first.Items.Single().ActualUnitPrice.Should().Be(11_000m);
        second.Items.Single().ActualUnitPrice.Should().Be(11_000m);
        await _orders.Received(1).ConsumeStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values =>
                values.Count == 1 && values[0] == new StockReservation(productId, 2)),
            default);
        await _orders.Received(1).ReleaseStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values =>
                values.Count == 1 && values[0] == new StockReservation(productId, 2)),
            default);
    }

    [Fact]
    public async Task Handle_RoundingTie_UsesOrderIdThenItemIdAsync()
    {
        var productId = Guid.NewGuid();
        var orders = Enumerable.Range(0, 3)
            .Select(_ => CreateOrder(OrderStatus.Batched, productId, 1))
            .OrderBy(order => order.Id)
            .ToArray();
        ConfigureOrders(orders.Reverse().ToArray());

        await CreateSut().Handle(
            CreateEvent(
                orders.Select(order => order.Id).Reverse().ToArray(),
                [new ProcurementPurchaseActual(productId, 1, 10_000m)]),
            default);

        orders[0].Items.Single().ActualQuantity.Should().Be(0.34m);
        orders[1].Items.Single().ActualQuantity.Should().Be(0.33m);
        orders[2].Items.Single().ActualQuantity.Should().Be(0.33m);
        orders.Sum(order => order.Items.Single().ActualQuantity).Should().Be(1m);
    }

    [Fact]
    public async Task Handle_ZeroPurchase_ReleasesEverythingAndAllocatesZeroAsync()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(OrderStatus.Batched, productId, 4);
        ConfigureOrders(order);

        await CreateSut().Handle(
            CreateEvent(
                [order.Id],
                [new ProcurementPurchaseActual(productId, 0, null)]),
            default);

        order.Status.Should().Be(OrderStatus.AtHub);
        order.Items.Single().ActualQuantity.Should().Be(0m);
        order.Items.Single().ActualUnitPrice.Should().BeNull();
        await _orders.Received(1).ReleaseStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values =>
                values.Count == 1 && values[0] == new StockReservation(productId, 4)),
            default);
    }

    [Fact]
    public async Task Handle_RepeatedEvent_FinalizesStockOnceAsync()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(OrderStatus.Batched, productId, 2);
        ConfigureOrders(order);
        var notification = CreateEvent(
            [order.Id],
            [new ProcurementPurchaseActual(productId, 1, 9_000m)]);
        var sut = CreateSut();

        await sut.Handle(notification, default);
        await sut.Handle(notification, default);

        order.Status.Should().Be(OrderStatus.AtHub);
        order.Items.Single().ActualQuantity.Should().Be(1m);
        await _orders.Received(1).ConsumeStockAsync(
            Arg.Any<IReadOnlyList<StockReservation>>(),
            default);
        await _orders.Received(1).ReleaseStockAsync(
            Arg.Any<IReadOnlyList<StockReservation>>(),
            default);
    }

    [Fact]
    public async Task Handle_ReservationFailure_DoesNotMutateOrderAsync()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(OrderStatus.Batched, productId, 2);
        ConfigureOrders(order);
        _orders.ReleaseStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(),
                default)
            .Returns(false);

        var act = () => CreateSut().Handle(
            CreateEvent(
                [order.Id],
                [new ProcurementPurchaseActual(productId, 1, 10_000m)]),
            default);

        await act.Should().ThrowAsync<ProcurementHandoverRejectedException>()
            .Where(exception => exception.Code == "STOCK_RESERVATION_CONFLICT");

        order.Status.Should().Be(OrderStatus.Batched);
        order.Items.Single().ActualQuantity.Should().BeNull();
        order.Items.Single().ActualUnitPrice.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LegacyEvent_ConsumesFullReservationWithoutActualSnapshotAsync()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(OrderStatus.Batched, productId, 2);
        ConfigureOrders(order);

        await CreateSut().Handle(CreateEvent([order.Id]), default);

        order.Status.Should().Be(OrderStatus.AtHub);
        order.Items.Single().ActualQuantity.Should().BeNull();
        await _orders.Received(1).ConsumeStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values =>
                values.Count == 1 && values[0] == new StockReservation(productId, 2)),
            default);
    }

    [Fact]
    public async Task Handle_RepeatedHandover_DoesNotRaiseSecondShortfallEventAsync()
    {
        // Arrange — C1: FinalizeAsync is called directly (bypassing IPublisher) so the returned
        // domain events from each call can be inspected without a publisher mock.
        var productId = Guid.NewGuid();
        var order = CreateConfirmedBatchedOrder(productId, quantity: 4, unitPrice: 20_000m);
        ConfigureOrders(order);
        var notification = CreateEvent(
            [order.Id],
            [new ProcurementPurchaseActual(productId, 2, 20_000m)]); // shortfall of 2
        var sut = CreateSut();

        // Act
        var firstEvents = await sut.FinalizeAsync(notification, default);
        var secondEvents = await sut.FinalizeAsync(notification, default);

        // Assert — order is AtHub after the first call, so ApplyProcurementActuals never runs again
        firstEvents.OfType<OrderProcurementShortfallDomainEvent>().Should().ContainSingle();
        secondEvents.OfType<OrderProcurementShortfallDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MissingCoveredOrder_DoesNotTouchStockAsync()
    {
        _orders.FindByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                default)
            .Returns([]);

        var act = () => CreateSut().Handle(CreateEvent([Guid.NewGuid()]), default);

        await act.Should().ThrowAsync<ProcurementHandoverRejectedException>()
            .Where(exception => exception.Code == "PROCUREMENT_ORDER_MISSING");

        await _orders.DidNotReceiveWithAnyArgs().ConsumeStockAsync(default!, default);
        await _orders.DidNotReceiveWithAnyArgs().ReleaseStockAsync(default!, default);
    }

    private void ConfigureOrders(params Order[] values)
    {
        _orders.FindByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                default)
            .Returns(values);
    }

    private ProcurementBatchHandedOffIntegrationEventHandler CreateSut() =>
        new(
            _orders,
            Substitute.For<IPublisher>(),
            Substitute.For<ILogger<ProcurementBatchHandedOffIntegrationEventHandler>>());

    private static ProcurementBatchHandedOffIntegrationEvent CreateEvent(
        IReadOnlyList<Guid> orderIds,
        IReadOnlyList<ProcurementPurchaseActual>? actuals = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc),
            orderIds,
            PurchaseActuals: actuals);

    /// <summary>
    /// Unlike <see cref="CreateOrder"/> (which reflects the status in directly), this goes
    /// through the real Confirm() flow so the item has a LockedUnitPrice — required for C1's
    /// shortfall event to fire.
    /// </summary>
    private static Order CreateConfirmedBatchedOrder(Guid marketProductId, int quantity, decimal unitPrice)
    {
        var order = new Order(Guid.NewGuid(), DateTime.UtcNow, null);
        order.AddItem(marketProductId, "Cà chua", quantity, unitPrice);
        order.ApplyConfirmationPricing(
            new Dictionary<Guid, FreshFlow.Orders.Domain.Entities.OrderItemTaxSnapshot>
            {
                [marketProductId] = new("KCT", 0m)
            },
            0m, 0m);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        order.ClearDomainEvents();
        return order;
    }

    private static Order CreateOrder(
        OrderStatus status,
        Guid marketProductId,
        int quantity)
    {
        var order = new Order(Guid.NewGuid(), DateTime.UtcNow, null);
        order.AddItem(marketProductId, "Cà chua", quantity, 20_000m);
        typeof(Order)
            .GetField(
                $"<{nameof(Order.Status)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(order, status);
        order.ClearDomainEvents();
        return order;
    }
}
