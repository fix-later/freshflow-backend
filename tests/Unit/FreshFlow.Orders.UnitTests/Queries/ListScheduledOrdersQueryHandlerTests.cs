using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.ListScheduledOrders;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListScheduledOrdersQueryHandlerTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ListScheduledOrdersQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public ListScheduledOrdersQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new ListScheduledOrdersQueryHandler(_scheduledOrderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantUserWithoutRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(new ListScheduledOrdersQuery(
            UserId, IsAdmin: false, RestaurantId: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _scheduledOrderRepository.DidNotReceive().SearchAsync(
            Arg.Any<ScheduledOrderSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RestaurantUserFilteringDifferentRestaurant_ReturnsForbiddenAsync()
    {
        var result = await _sut.Handle(new ListScheduledOrdersQuery(
            UserId, IsAdmin: false, OtherRestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_RestaurantUser_UsesOwnedRestaurantAndReturnsPagedSchedulesAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.SearchAsync(Arg.Any<ScheduledOrderSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(([scheduledOrder], 1));

        var result = await _sut.Handle(new ListScheduledOrdersQuery(
            UserId,
            IsAdmin: false,
            RestaurantId: null,
            IncludeCancelled: false,
            Page: 2,
            PageSize: 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Meta.Total.Should().Be(1);
        result.Value.Meta.Page.Should().Be(2);
        result.Value.Data.Should().ContainSingle();
        result.Value.Data.Single().RecurrenceType.Should().Be("daily");
        await _scheduledOrderRepository.Received(1).SearchAsync(
            Arg.Is<ScheduledOrderSearchCriteria>(c =>
                c.RestaurantId == RestaurantId
                && !c.IncludeCancelled
                && c.Page == 2
                && c.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminCanListAllWithoutOwnershipLookupAsync()
    {
        _scheduledOrderRepository.SearchAsync(Arg.Any<ScheduledOrderSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ScheduledOrder>(), 0));

        var result = await _sut.Handle(new ListScheduledOrdersQuery(
            UserId,
            IsAdmin: true,
            RestaurantId: null,
            IncludeCancelled: true), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
        await _scheduledOrderRepository.Received(1).SearchAsync(
            Arg.Is<ScheduledOrderSearchCriteria>(c =>
                c.RestaurantId == null
                && c.IncludeCancelled),
            Arg.Any<CancellationToken>());
    }

    private static ScheduledOrder NewSchedule(Guid restaurantId) =>
        new(restaurantId, RecurrenceType.Daily, DateTime.UtcNow.AddDays(1), "note");
}
