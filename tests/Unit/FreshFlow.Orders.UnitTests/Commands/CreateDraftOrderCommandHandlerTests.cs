using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateDraftOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();
    private readonly IOperationalSettingsRepository _operationalSettings = Substitute.For<IOperationalSettingsRepository>();

    private readonly CreateDraftOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public CreateDraftOrderCommandHandlerTests()
    {
        _sut = new CreateDraftOrderCommandHandler(
            _orderRepository, _restaurantReader, _marketProductReader, _operationalSettings);

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 50));

        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(OperationalSettings.CreateDefault());
    }

    private static CreateDraftOrderCommand Cmd(
        Guid? marketProductId = null, int quantity = 5, Guid? userId = null) =>
        new(
            userId ?? UserId,
            [new DraftOrderItemRequest(marketProductId ?? MarketProductId, quantity)],
            ScheduledFor: null,
            Notes: null);

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsForbiddenAsync()
    {
        // Arrange
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_RestaurantNotApproved_ReturnsRestaurantNotApprovedAsync()
    {
        // Arrange
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: false));

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_APPROVED");
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidProduct_ReturnsInvalidProductErrorAsync()
    {
        // Arrange
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PRODUCT");
    }

    [Fact]
    public async Task Handle_InsufficientStock_ReturnsInsufficientStockErrorAsync()
    {
        // Arrange
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 2));

        // Act
        var result = await _sut.Handle(Cmd(quantity: 5), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_DuplicateProductLinesExceedAvailableStock_ReturnsInsufficientStockErrorAsync()
    {
        // Arrange
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Cà chua", 20_000m, AvailableQuantity: 10));
        var command = new CreateDraftOrderCommand(
            UserId,
            [
                new DraftOrderItemRequest(MarketProductId, 6),
                new DraftOrderItemRequest(MarketProductId, 5)
            ],
            ScheduledFor: null,
            Notes: null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_ScheduledForInThePast_ReturnsDeliveryDateOutOfWindowAsync()
    {
        // Arrange
        var command = new CreateDraftOrderCommand(
            UserId,
            [new DraftOrderItemRequest(MarketProductId, 5)],
            ScheduledFor: DateTime.UtcNow.AddDays(-1),
            Notes: null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ScheduledForBeyondDPlus7_ReturnsDeliveryDateOutOfWindowAsync()
    {
        // Arrange
        var command = new CreateDraftOrderCommand(
            UserId,
            [new DraftOrderItemRequest(MarketProductId, 5)],
            ScheduledFor: DateTime.UtcNow.AddDays(8),
            Notes: null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
    }

    [Fact]
    public async Task Handle_ScheduledForBeyondDefaultWindow_ConfiguredFourteenDayWindow_SucceedsAsync()
    {
        // Arrange — the same D+8 date that Handle_ScheduledForBeyondDPlus7... rejects under the
        // default 7-day window is admitted once the admin has configured a 14-day window.
        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new OperationalSettings(new TimeOnly(22, 0), true, "hub_relay", 14));
        var command = new CreateDraftOrderCommand(
            UserId,
            [new DraftOrderItemRequest(MarketProductId, 5)],
            ScheduledFor: DateTime.UtcNow.AddDays(8),
            Notes: null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ScheduledForWithinWindow_SucceedsAsync()
    {
        // Arrange
        var command = new CreateDraftOrderCommand(
            UserId,
            [new DraftOrderItemRequest(MarketProductId, 5)],
            ScheduledFor: DateTime.UtcNow.AddDays(3),
            Notes: null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Success_CreatesOrderWithCorrectTotalAsync()
    {
        // Act
        var result = await _sut.Handle(Cmd(quantity: 5), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.Status.Should().Be("draft");
        result.Value.PaymentStatus.Should().Be("not_applicable");
        result.Value.TotalAmount.Should().Be(100_000m);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ProductNameSnapshot.Should().Be("Cà chua");
        result.Value.Items[0].UnitPrice.Should().Be(20_000m);
    }

    [Fact]
    public async Task Handle_Success_PersistsViaAddAndSaveChangesAsync()
    {
        // Act
        await _sut.Handle(Cmd(), default);

        // Assert
        await _orderRepository.Received(1).AddAsync(
            Arg.Is<Order>(o => o.RestaurantId == RestaurantId && o.Items.Count == 1),
            Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
