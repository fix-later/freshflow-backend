using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.CancelOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CancelOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly CancelOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public CancelOrderCommandHandlerTests()
    {
        _orderRepository.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _orderRepository.ReleaseStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _creditService.RefundAsync(
                RestaurantId, Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<RestaurantCreditDto>.Success(
                new RestaurantCreditDto(RestaurantId, 1_000m, 0m, 1_000m, DateTime.UtcNow)));
        _sut = new CancelOrderCommandHandler(_orderRepository, _restaurantReader, _creditService);
    }

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(Cmd(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserCancellingAnotherRestaurantsOrder_ReturnsForbiddenAsync()
    {
        var order = NewDraftOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        _orderRepository.DidNotReceive().Track(order);
    }

    [Fact]
    public async Task Handle_BatchedOrder_ReturnsOrderNotCancellableAsync()
    {
        var order = NewConfirmedOrder(RestaurantId);
        order.AdvanceStatus(OrderStatus.Batched);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_CANCELLABLE");
        order.Status.Should().Be(OrderStatus.Batched);
        await _creditService.DidNotReceive().RefundAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DraftOrder_CancelsAndPersistsWithoutCreditRefundAsync()
    {
        var order = NewDraftOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id, reason: "Khách hàng đổi ý"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("cancelled");
        result.Value.CancellationReason.Should().Be("Khách hàng đổi ý");
        order.Status.Should().Be(OrderStatus.Cancelled);
        _orderRepository.Received(1).Track(order);
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _creditService.DidNotReceive().RefundAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConfirmedOrder_CancelsRefundsCreditAndLetsCreditSavePersistAsync()
    {
        var order = NewConfirmedOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("cancelled");
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.PaymentStatus.Should().Be(OrderPaymentStatus.Waived);
        _orderRepository.Received(1).Track(order);
        await _orderRepository.Received(1).ReleaseStockAsync(
            Arg.Is<IReadOnlyList<StockReservation>>(values => values.Count == 1 && values[0].Quantity == 5),
            Arg.Any<CancellationToken>());
        await _creditService.Received(1).RefundAsync(
            RestaurantId, order.Id, 100_000m, "Order cancelled", Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminCanCancelAnyRestaurantOrderWithoutOwnershipLookupAsync()
    {
        var order = NewDraftOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new CancelOrderCommand(UserId, IsAdmin: true, order.Id, Reason: null), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreditRefundFails_ReturnsCreditErrorWithoutSavingOrderDirectlyAsync()
    {
        var order = NewConfirmedOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _creditService.RefundAsync(
                RestaurantId, order.Id, order.TotalAmount, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<RestaurantCreditDto>.Failure(
                Error.Validation("CREDIT_REFUND_EXCEEDS_BALANCE", "Refund amount cannot exceed balance.")));

        var result = await _sut.Handle(Cmd(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_REFUND_EXCEEDS_BALANCE");
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConfirmedOrderCalledTwice_ReleasesAndRefundsOnceAsync()
    {
        var order = NewConfirmedOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var first = await _sut.Handle(Cmd(order.Id), default);
        var second = await _sut.Handle(Cmd(order.Id), default);

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("ORDER_NOT_CANCELLABLE");
        await _orderRepository.Received(1).ReleaseStockAsync(
            Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>());
        await _creditService.Received(1).RefundAsync(
            RestaurantId, order.Id, order.TotalAmount, Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    private static CancelOrderCommand Cmd(Guid orderId, string? reason = null) =>
        new(UserId, IsAdmin: false, orderId, reason);

    private static Order NewDraftOrder(Guid restaurantId) =>
        new(restaurantId, scheduledFor: null, notes: null);

    private static Order NewConfirmedOrder(Guid restaurantId)
    {
        var order = NewDraftOrder(restaurantId);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        order.Confirm();
        return order;
    }
}
