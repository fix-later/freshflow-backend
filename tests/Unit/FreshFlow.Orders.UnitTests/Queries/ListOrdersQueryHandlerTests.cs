using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.ListOrders;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListOrdersQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ListOrdersQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ListOrdersQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new ListOrdersQueryHandler(_orderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantUserWithoutRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(new ListOrdersQuery(
            UserId, IsAdmin: false, RestaurantId: null, Status: null, From: null, To: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _orderRepository.DidNotReceive().SearchAsync(
            Arg.Any<OrderSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RestaurantUserFilteringDifferentRestaurant_ReturnsForbiddenAsync()
    {
        var result = await _sut.Handle(new ListOrdersQuery(
            UserId, IsAdmin: false, OtherRestaurantId, Status: null, From: null, To: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _orderRepository.DidNotReceive().SearchAsync(
            Arg.Any<OrderSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RestaurantUser_UsesOwnedRestaurantAndReturnsPagedOrdersAsync()
    {
        var order = NewConfirmedOrder(RestaurantId);
        _orderRepository.SearchAsync(Arg.Any<OrderSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(([order], 1));

        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var expectedTo = to.Date.AddDays(1).AddTicks(-1);

        var result = await _sut.Handle(new ListOrdersQuery(
            UserId,
            IsAdmin: false,
            RestaurantId: null,
            Status: "confirmed",
            From: from,
            To: to,
            Sort: "createdAt:asc",
            Page: 2,
            PageSize: 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Meta.Total.Should().Be(1);
        result.Value.Meta.Page.Should().Be(2);
        result.Value.Meta.PageSize.Should().Be(10);
        result.Value.Data.Should().ContainSingle();
        result.Value.Data.Single().Status.Should().Be("confirmed");
        result.Value.Data.Single().ItemCount.Should().Be(1);
        await _orderRepository.Received(1).SearchAsync(
            Arg.Is<OrderSearchCriteria>(c =>
                c.RestaurantId == RestaurantId
                && c.Status == OrderStatus.Confirmed
                && c.CreatedFrom == from
                && c.CreatedTo == expectedTo
                && c.SortAscending
                && c.Page == 2
                && c.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminCanFilterAnyRestaurantWithoutOwnershipLookupAsync()
    {
        _orderRepository.SearchAsync(Arg.Any<OrderSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Order>(), 0));

        var result = await _sut.Handle(new ListOrdersQuery(
            UserId,
            IsAdmin: true,
            RestaurantId: OtherRestaurantId,
            Status: "in_transit",
            From: null,
            To: null,
            Page: 1,
            PageSize: 20), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).SearchAsync(
            Arg.Is<OrderSearchCriteria>(c =>
                c.RestaurantId == OtherRestaurantId
                && c.Status == OrderStatus.Delivering
                && !c.SortAscending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsValidationErrorWithoutQueryingAsync()
    {
        var result = await _sut.Handle(new ListOrdersQuery(
            UserId, IsAdmin: true, RestaurantId: null, Status: "unknown", From: null, To: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _orderRepository.DidNotReceive().SearchAsync(
            Arg.Any<OrderSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    private static Order NewConfirmedOrder(Guid restaurantId)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 3, 20_000m);
        order.Confirm();
        return order;
    }
}
