using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.ConfirmOrderReceipt;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ConfirmOrderReceiptCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ConfirmOrderReceiptCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ConfirmOrderReceiptCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new ConfirmOrderReceiptCommandHandler(_orderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new ConfirmOrderReceiptCommand(UserId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderOwnedByAnotherRestaurant_ReturnsForbiddenAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderReceiptCommand(UserId, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderNotDelivered_ReturnsOrderNotDeliveredAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Confirmed, RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderReceiptCommand(UserId, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DELIVERED");
    }

    [Fact]
    public async Task Handle_AlreadyConfirmedReceipt_ReturnsConflictAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        order.ConfirmReceipt();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderReceiptCommand(UserId, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_RECEIPT_ALREADY_CONFIRMED");
    }

    [Fact]
    public async Task Handle_DeliveredOrder_ConfirmsReceiptAndPersistsAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderReceiptCommand(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConfirmedReceiptAt.Should().NotBeNull();
        order.ConfirmedReceiptAt.Should().NotBeNull();
        _orderRepository.Received(1).Track(order);
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Order NewOrderInStatus(OrderStatus status, Guid restaurantId)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Tomato", 2, 20_000m);
        order.Confirm();

        var pipeline = new[]
        {
            OrderStatus.Confirmed,
            OrderStatus.Batched,
            OrderStatus.PickedUp,
            OrderStatus.AtHub,
            OrderStatus.Delivering,
            OrderStatus.Delivered
        };

        var targetIndex = Array.IndexOf(pipeline, status);
        for (var i = 1; i <= targetIndex; i++)
            order.AdvanceStatus(pipeline[i]);

        return order;
    }
}
