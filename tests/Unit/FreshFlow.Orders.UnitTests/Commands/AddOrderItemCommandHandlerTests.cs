using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.AddOrderItem;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AddOrderItemCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();

    private readonly AddOrderItemCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public AddOrderItemCommandHandlerTests()
    {
        _sut = new AddOrderItemCommandHandler(_orderRepository, _restaurantReader, _marketProductReader);

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 50));
    }

    private static Order NewDraftOrder(Guid? restaurantId = null) =>
        new(restaurantId ?? RestaurantId, scheduledFor: null, notes: null);

    private static AddOrderItemCommand Cmd(Guid orderId, Guid? marketProductId = null, int quantity = 5) =>
        new(UserId, orderId, marketProductId ?? MarketProductId, quantity);

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(Cmd(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderBelongsToAnotherRestaurant_ReturnsForbiddenAsync()
    {
        // Arrange
        var order = NewDraftOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(Cmd(order.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_InvalidProduct_ReturnsInvalidProductErrorAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(Cmd(order.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PRODUCT");
    }

    [Fact]
    public async Task Handle_InsufficientStock_ReturnsInsufficientStockErrorAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 2));

        // Act
        var result = await _sut.Handle(Cmd(order.Id, quantity: 5), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_ExistingSameProductPlusAddedQuantityExceedsStock_ReturnsInsufficientStockErrorAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        order.AddItem(MarketProductId, "Cà chua", 8, 20_000m);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 10));

        // Act
        var result = await _sut.Handle(Cmd(order.Id, quantity: 3), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_OrderNotDraft_ReturnsOrderNotDraftErrorAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        order.AddItem(Guid.NewGuid(), "Hành lá", 1, 5_000m);
        order.Confirm();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(Cmd(order.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public async Task Handle_Success_AddsItemAndPersistsAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(Cmd(order.Id, quantity: 3), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.TotalAmount.Should().Be(60_000m);
        _orderRepository.Received(1).TrackNewItem(Arg.Any<OrderItem>());
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
