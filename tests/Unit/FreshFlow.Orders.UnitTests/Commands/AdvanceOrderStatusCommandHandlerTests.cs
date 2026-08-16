using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.AdvanceOrderStatus;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AdvanceOrderStatusCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly AdvanceOrderStatusCommandHandler _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public AdvanceOrderStatusCommandHandlerTests() =>
        _sut = new AdvanceOrderStatusCommandHandler(_orderRepository);

    [Fact]
    public async Task Handle_ConfirmedToBatched_AdvancesAndPersistsAsync()
    {
        var order = NewOrderInStatus(OrderStatus.Confirmed);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new AdvanceOrderStatusCommand(order.Id, "batched"), default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Batched);
        _orderRepository.Received(1).Track(order);
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("delivering")]
    [InlineData("delivered")]
    [InlineData("confirmed")]
    [InlineData("garbage")]
    public async Task Handle_NonAdvanceableTarget_ReturnsValidationAndDoesNotPersistAsync(string status)
    {
        var result = await _sut.Handle(new AdvanceOrderStatusCommand(Guid.NewGuid(), status), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_STATUS_NOT_ADVANCEABLE");
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidTransition_ReturnsConflictFromDomainAsync()
    {
        // Order is still Draft — cannot jump to Batched.
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new AdvanceOrderStatusCommand(order.Id, "batched"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_INVALID_TRANSITION");
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new AdvanceOrderStatusCommand(Guid.NewGuid(), "batched"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    private static Order NewOrderInStatus(OrderStatus status)
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        order.Confirm();

        var pipeline = new[]
        {
            OrderStatus.Confirmed, OrderStatus.Batched, OrderStatus.PickedUp,
            OrderStatus.AtHub, OrderStatus.Delivering, OrderStatus.Delivered
        };

        var targetIndex = Array.IndexOf(pipeline, status);
        for (var i = 1; i <= targetIndex; i++)
            order.AdvanceStatus(pipeline[i]);

        return order;
    }
}
