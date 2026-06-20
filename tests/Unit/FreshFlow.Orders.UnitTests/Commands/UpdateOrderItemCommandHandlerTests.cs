using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.UpdateOrderItem;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateOrderItemCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();

    private readonly UpdateOrderItemCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public UpdateOrderItemCommandHandlerTests()
    {
        _sut = new UpdateOrderItemCommandHandler(_orderRepository, _restaurantReader, _marketProductReader);

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 50));
    }

    private static Order NewDraftOrderWithItem(Guid? restaurantId = null, int quantity = 5)
    {
        var order = new Order(restaurantId ?? RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity, 20_000m);
        return order;
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(new UpdateOrderItemCommand(UserId, Guid.NewGuid(), Guid.NewGuid(), 10), default);

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
            new UpdateOrderItemCommand(UserId, order.Id, order.Items.Single().Id, 10), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ItemNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(
            new UpdateOrderItemCommand(UserId, order.Id, Guid.NewGuid(), 10), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_InvalidProduct_ReturnsInvalidProductErrorAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(
            new UpdateOrderItemCommand(UserId, order.Id, order.Items.Single().Id, 10), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PRODUCT");
    }

    [Fact]
    public async Task Handle_IncreasedQuantityExceedsStock_ReturnsInsufficientStockErrorAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 8));

        // Act
        var result = await _sut.Handle(
            new UpdateOrderItemCommand(UserId, order.Id, order.Items.Single().Id, 10), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_OtherSameProductLinesPlusUpdatedQuantityExceedsStock_ReturnsInsufficientStockErrorAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem(quantity: 5);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        var itemToUpdate = order.Items.First();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 12));

        // Act
        var result = await _sut.Handle(
            new UpdateOrderItemCommand(UserId, order.Id, itemToUpdate.Id, 8), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
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
        var result = await _sut.Handle(
            new UpdateOrderItemCommand(UserId, order.Id, itemId, 10), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public async Task Handle_Success_UpdatesQuantityAndPersistsAsync()
    {
        // Arrange
        var order = NewDraftOrderWithItem(quantity: 5);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(
            new UpdateOrderItemCommand(UserId, order.Id, order.Items.Single().Id, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Single().Quantity.Should().Be(10);
        result.Value.TotalAmount.Should().Be(200_000m);
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
