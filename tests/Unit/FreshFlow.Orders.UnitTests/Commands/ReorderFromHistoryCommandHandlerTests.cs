using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.ReorderFromHistory;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReorderFromHistoryCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();
    private readonly ReorderFromHistoryCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ReorderFromHistoryCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Fresh tomato", 22_000m, 20));
        _sut = new ReorderFromHistoryCommandHandler(_orderRepository, _restaurantReader, _marketProductReader);
    }

    [Fact]
    public async Task Handle_SourceOrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(Cmd(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_SourceOrderOwnedByAnotherRestaurant_ReturnsForbiddenAsync()
    {
        var source = NewSourceOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var result = await _sut.Handle(Cmd(source.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_SourceOrderWithoutItems_ReturnsOrderEmptyAsync()
    {
        var source = new Order(RestaurantId, scheduledFor: null, notes: null);
        _orderRepository.FindByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var result = await _sut.Handle(Cmd(source.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_EMPTY");
    }

    [Fact]
    public async Task Handle_ScheduledForOutOfWindow_ReturnsDeliveryDateOutOfWindowAsync()
    {
        var source = NewSourceOrder(RestaurantId);
        _orderRepository.FindByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var result = await _sut.Handle(
            Cmd(source.Id, scheduledFor: DateTime.UtcNow.AddDays(8)), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
    }

    [Fact]
    public async Task Handle_CurrentProductMissing_ReturnsInvalidProductAsync()
    {
        var source = NewSourceOrder(RestaurantId);
        _orderRepository.FindByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(Cmd(source.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PRODUCT");
    }

    [Fact]
    public async Task Handle_InsufficientCurrentStock_ReturnsInsufficientStockAsync()
    {
        var source = NewSourceOrder(RestaurantId);
        _orderRepository.FindByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Fresh tomato", 22_000m, 1));

        var result = await _sut.Handle(Cmd(source.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_ValidSource_CreatesDraftUsingCurrentProductSnapshotAsync()
    {
        var source = NewSourceOrder(RestaurantId);
        var scheduledFor = DateTime.UtcNow.AddDays(2);
        _orderRepository.FindByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var result = await _sut.Handle(Cmd(source.Id, scheduledFor, notes: "new order"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("draft");
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.ScheduledFor.Should().Be(scheduledFor);
        result.Value.Notes.Should().Be("new order");
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().ProductNameSnapshot.Should().Be("Fresh tomato");
        result.Value.Items.Single().UnitPrice.Should().Be(22_000m);
        await _orderRepository.Received(1).AddAsync(
            Arg.Is<Order>(o =>
                o.RestaurantId == RestaurantId
                && o.Items.Count == 1
                && o.Items.Single().ProductNameSnapshot == "Fresh tomato"
                && o.Items.Single().UnitPrice == 22_000m),
            Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ReorderFromHistoryCommand Cmd(
        Guid sourceOrderId,
        DateTime? scheduledFor = null,
        string? notes = null) =>
        new(UserId, sourceOrderId, scheduledFor, notes);

    private static Order NewSourceOrder(Guid restaurantId)
    {
        var source = new Order(restaurantId, scheduledFor: null, notes: "old note");
        source.AddItem(MarketProductId, "Old tomato", 2, 20_000m);
        return source;
    }
}
