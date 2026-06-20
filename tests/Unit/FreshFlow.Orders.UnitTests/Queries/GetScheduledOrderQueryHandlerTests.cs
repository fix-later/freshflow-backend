using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetScheduledOrder;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetScheduledOrderQueryHandlerTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GetScheduledOrderQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public GetScheduledOrderQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GetScheduledOrderQueryHandler(_scheduledOrderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_ScheduledOrderMissing_ReturnsNotFoundAsync()
    {
        _scheduledOrderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new GetScheduledOrderQuery(
            UserId, IsAdmin: false, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserRequestingAnotherRestaurantsSchedule_ReturnsForbiddenAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new GetScheduledOrderQuery(
            UserId, IsAdmin: false, scheduledOrder.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_OwnerRestaurant_ReturnsScheduleAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new GetScheduledOrderQuery(
            UserId, IsAdmin: false, scheduledOrder.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduledOrderId.Should().Be(scheduledOrder.Id);
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.RecurrenceType.Should().Be("weekly");
    }

    [Fact]
    public async Task Handle_AdminCanReadAnyScheduleWithoutOwnershipLookupAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new GetScheduledOrderQuery(
            UserId, IsAdmin: true, scheduledOrder.Id), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    private static ScheduledOrder NewSchedule(Guid restaurantId) =>
        new(restaurantId, RecurrenceType.Weekly, DateTime.UtcNow.AddDays(7), "note");
}
