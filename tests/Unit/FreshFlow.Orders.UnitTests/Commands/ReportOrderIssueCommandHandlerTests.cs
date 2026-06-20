using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.ReportOrderIssue;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReportOrderIssueCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IOrderIssueRepository _orderIssueRepository = Substitute.For<IOrderIssueRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ReportOrderIssueCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ReportOrderIssueCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new ReportOrderIssueCommandHandler(_orderRepository, _orderIssueRepository, _restaurantReader);
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
    public async Task Handle_OrderOwnedByAnotherRestaurant_ReturnsForbiddenAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _orderIssueRepository.DidNotReceive().AddAsync(
            Arg.Any<OrderIssue>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderNotDelivered_ReturnsIssueNotAllowedAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Confirmed, RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ISSUE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Handle_InvalidIssueType_ReturnsValidationErrorAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id, issueType: "late"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_AffectedQuantityNotPositive_ReturnsInvalidIssueQuantityAsync(decimal affectedQuantity)
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id, affectedQuantity: affectedQuantity), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ISSUE_QUANTITY");
    }

    [Fact]
    public async Task Handle_ItemMissing_ReturnsOrderItemNotFoundAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id, orderItemId: Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AffectedQuantityExceedsItemQuantity_ReturnsInvalidIssueQuantityAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        var itemId = order.Items.Single().Id;
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id, itemId, affectedQuantity: 3m), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ISSUE_QUANTITY");
    }

    [Fact]
    public async Task Handle_ValidIssue_PersistsAndReturnsDtoAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Delivered, RestaurantId);
        var itemId = order.Items.Single().Id;
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Cmd(order.Id, itemId, affectedQuantity: 1m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(order.Id);
        result.Value.OrderItemId.Should().Be(itemId);
        result.Value.IssueType.Should().Be("damaged");
        result.Value.Status.Should().Be("open");
        await _orderIssueRepository.Received(1).AddAsync(
            Arg.Is<OrderIssue>(i =>
                i.OrderId == order.Id
                && i.OrderItemId == itemId
                && i.ReportedBy == UserId
                && i.IssueType == OrderIssueType.Damaged
                && i.AffectedQuantity == 1m),
            Arg.Any<CancellationToken>());
        await _orderIssueRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ReportOrderIssueCommand Cmd(
        Guid orderId,
        Guid? orderItemId = null,
        decimal affectedQuantity = 1m,
        string issueType = "damaged") =>
        new(UserId, orderId, orderItemId, issueType, affectedQuantity, "damaged packaging");

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
