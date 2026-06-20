using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.RecordOrderItemActualQuantity;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RecordOrderItemActualQuantityCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly RecordOrderItemActualQuantityCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public RecordOrderItemActualQuantityCommandHandlerTests() =>
        _sut = new RecordOrderItemActualQuantityCommandHandler(_orderRepository);

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(
            new RecordOrderItemActualQuantityCommand(UserId, Guid.NewGuid(), Guid.NewGuid(), 4m), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ItemMissing_ReturnsOrderItemNotFoundAsync()
    {
        var order = NewConfirmedOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new RecordOrderItemActualQuantityCommand(UserId, order.Id, Guid.NewGuid(), 4m), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DraftOrder_ReturnsOrderCannotAdjustAsync()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        var itemId = order.Items.Single().Id;
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new RecordOrderItemActualQuantityCommand(UserId, order.Id, itemId, 4m), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_CANNOT_ADJUST");
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActualQuantityExceedsOrderedQuantity_ReturnsInvalidActualQuantityAsync()
    {
        var order = NewConfirmedOrder();
        var itemId = order.Items.Single().Id;
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new RecordOrderItemActualQuantityCommand(UserId, order.Id, itemId, 6m), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ACTUAL_QUANTITY");
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Batched)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.AtHub)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Handle_AdjustableStatus_RecordsActualQuantityAndPersistsAsync(OrderStatus status)
    {
        var order = NewOrderInStatus(status);
        var itemId = order.Items.Single().Id;
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new RecordOrderItemActualQuantityCommand(UserId, order.Id, itemId, 4.5m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Single().ActualQuantity.Should().Be(4.5m);
        order.Items.Single().ActualQuantity.Should().Be(4.5m);
        _orderRepository.Received(1).Track(order);
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Order NewConfirmedOrder() => NewOrderInStatus(OrderStatus.Confirmed);

    private static Order NewOrderInStatus(OrderStatus status)
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
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
