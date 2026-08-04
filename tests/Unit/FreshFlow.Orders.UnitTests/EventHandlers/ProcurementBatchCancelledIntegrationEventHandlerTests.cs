using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchCancelledIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ProcurementBatchCancelledIntegrationEventHandlerTests()
    {
        _orders.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _orders.ReleaseStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _creditService.RefundAsync(
                RestaurantId,
                Arg.Any<Guid>(),
                Arg.Any<decimal>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<CreditRefundDto>.Success(new CreditRefundDto(
                Guid.NewGuid(),
                new RestaurantCreditDto(RestaurantId, 1_000_000m, 0m, 1_000_000m, DateTime.UtcNow))));
    }

    [Fact]
    public async Task Handle_RepeatedEvent_ReleasesReservationAndRefundsCreditOnceAsync()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var notification = new ProcurementBatchCancelledIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Market session cancelled",
            DateTime.UtcNow,
            [order.Id]);
        var sut = new ProcurementBatchCancelledIntegrationEventHandler(
            _orders,
            _creditService,
            Substitute.For<ILogger<ProcurementBatchCancelledIntegrationEventHandler>>());

        await sut.Handle(notification, default);
        await sut.Handle(notification, default);

        order.Status.Should().Be(OrderStatus.Cancelled);
        await _orders.Received(1).ReleaseStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values =>
                values.Count == 1 &&
                values[0].MarketProductId == MarketProductId &&
                values[0].Quantity == 5),
            default);
        await _creditService.Received(1).RefundAsync(
            RestaurantId,
            order.Id,
            100_000m,
            "Procurement batch cancelled",
            default);
    }
}
