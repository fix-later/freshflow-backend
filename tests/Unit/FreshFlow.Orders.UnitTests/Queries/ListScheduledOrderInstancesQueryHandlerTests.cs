using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.ListScheduledOrderInstances;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListScheduledOrderInstancesQueryHandlerTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ListScheduledOrderInstancesQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ListScheduledOrderInstancesQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new ListScheduledOrderInstancesQueryHandler(
            _scheduledOrderRepository, _orderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_ScheduledOrderMissing_ReturnsNotFoundAsync()
    {
        _scheduledOrderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new ListScheduledOrderInstancesQuery(
            UserId, IsAdmin: false, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserRequestingAnotherRestaurantsInstances_ReturnsForbiddenAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new ListScheduledOrderInstancesQuery(
            UserId, IsAdmin: false, scheduledOrder.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _orderRepository.DidNotReceive().GetByScheduledOrderIdAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnerRestaurant_ReturnsPagedInstancesAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        var order = new Order(RestaurantId, DateTime.UtcNow.AddDays(1), "note", scheduledOrderId: scheduledOrder.Id);
        order.AddItem(MarketProductId, "Tomato", 2, 20_000m);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);
        _orderRepository.GetByScheduledOrderIdAsync(
                scheduledOrder.Id, 2, 10, Arg.Any<CancellationToken>())
            .Returns(([order], 1));

        var result = await _sut.Handle(new ListScheduledOrderInstancesQuery(
            UserId, IsAdmin: false, scheduledOrder.Id, Page: 2, PageSize: 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Meta.Total.Should().Be(1);
        result.Value.Meta.Page.Should().Be(2);
        result.Value.Data.Single().ScheduledOrderId.Should().Be(scheduledOrder.Id);
        result.Value.Data.Single().ItemCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AdminCanReadInstancesWithoutOwnershipLookupAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);
        _orderRepository.GetByScheduledOrderIdAsync(
                scheduledOrder.Id, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Order>(), 0));

        var result = await _sut.Handle(new ListScheduledOrderInstancesQuery(
            UserId, IsAdmin: true, scheduledOrder.Id), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    private static ScheduledOrder NewSchedule(Guid restaurantId) =>
        new(restaurantId, RecurrenceType.Daily, DateTime.UtcNow.AddDays(1), "note");
}
