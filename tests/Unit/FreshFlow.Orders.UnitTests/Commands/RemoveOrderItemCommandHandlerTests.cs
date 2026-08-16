using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.RemoveOrderItem;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RemoveOrderItemCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();

    private readonly RemoveOrderItemCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public RemoveOrderItemCommandHandlerTests()
    {
        _sut = new RemoveOrderItemCommandHandler(_orderRepository, _restaurantReader);

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
    }

    private static Order NewDraftOrderWithItem(Guid? restaurantId = null)
    {
        var order = new Order(restaurantId ?? RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        return order;
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(new RemoveOrderItemCommand(UserId, Guid.NewGuid(), Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderBelongsToAnotherRestaurant_ReturnsForbiddenAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(
            new RemoveOrderItemCommand(UserId, order.Id, order.Items.Single().Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_OrderNotDraft_ReturnsOrderNotDraftErrorAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem();
        var itemId = order.Items.Single().Id;
        order.Confirm();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(new RemoveOrderItemCommand(UserId, order.Id, itemId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public async Task Handle_ItemNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(new RemoveOrderItemCommand(UserId, order.Id, Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_Success_RemovesItemAndPersistsAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem();
        var itemId = order.Items.Single().Id;
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(new RemoveOrderItemCommand(UserId, order.Id, itemId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalAmount.Should().Be(0);
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
