using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.CancelScheduledOrder;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CancelScheduledOrderCommandHandlerTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly CancelScheduledOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public CancelScheduledOrderCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new CancelScheduledOrderCommandHandler(_scheduledOrderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_ScheduledOrderMissing_ReturnsNotFoundAsync()
    {
        _scheduledOrderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new CancelScheduledOrderCommand(
            UserId, IsAdmin: false, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserCancellingAnotherRestaurantsSchedule_ReturnsForbiddenAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new CancelScheduledOrderCommand(
            UserId, IsAdmin: false, scheduledOrder.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        _scheduledOrderRepository.DidNotReceive().Track(scheduledOrder);
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ReturnsConflictAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        scheduledOrder.Cancel();
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new CancelScheduledOrderCommand(
            UserId, IsAdmin: false, scheduledOrder.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_ALREADY_CANCELLED");
    }

    [Fact]
    public async Task Handle_ActiveSchedule_CancelsAndPersistsAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new CancelScheduledOrderCommand(
            UserId, IsAdmin: false, scheduledOrder.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CancelledAt.Should().NotBeNull();
        scheduledOrder.IsActive.Should().BeFalse();
        _scheduledOrderRepository.Received(1).Track(scheduledOrder);
        await _scheduledOrderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminCanCancelWithoutOwnershipLookupAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(new CancelScheduledOrderCommand(
            UserId, IsAdmin: true, scheduledOrder.Id), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    private static ScheduledOrder NewSchedule(Guid restaurantId) =>
        new(restaurantId, RecurrenceType.Daily, DateTime.UtcNow.AddDays(1), "note");
}
