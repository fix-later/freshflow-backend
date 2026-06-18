using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetOrder;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetOrderQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GetOrderQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public GetOrderQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GetOrderQueryHandler(_orderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserRequestingAnotherRestaurantsOrder_ReturnsForbiddenAsync()
    {
        var order = NewOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_OwnerRestaurant_ReturnsOrderDetailAsync()
    {
        var order = NewOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(order.Id);
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.Status.Should().Be("draft");
        result.Value.Items.Should().ContainSingle();
        result.Value.CreatedAt.Should().Be(order.CreatedAt);
        result.Value.UpdatedAt.Should().Be(order.UpdatedAt);
    }

    [Fact]
    public async Task Handle_AdminCanReadAnyOrderWithoutOwnershipLookupAsync()
    {
        var order = NewOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: true, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(OtherRestaurantId);
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    private static Order NewOrder(Guid restaurantId)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: "Giao sớm");
        order.AddItem(MarketProductId, "Cà chua", 3, 20_000m);
        return order;
    }
}
