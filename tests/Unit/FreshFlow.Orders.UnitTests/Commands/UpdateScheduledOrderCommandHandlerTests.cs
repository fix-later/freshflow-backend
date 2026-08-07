using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.UpdateScheduledOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateScheduledOrderCommandHandlerTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly UpdateScheduledOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid DeliveryAddressId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public UpdateScheduledOrderCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryAddressSourceDto(
                DeliveryAddressId, "Bếp trưởng", "0901234567", "1 Test Street", 10.123456m, 106.123456m));
        _sut = new UpdateScheduledOrderCommandHandler(_scheduledOrderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_ScheduledOrderMissing_ReturnsNotFoundAsync()
    {
        _scheduledOrderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(Cmd(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserUpdatingAnotherRestaurantsSchedule_ReturnsForbiddenAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(Cmd(scheduledOrder.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        _scheduledOrderRepository.DidNotReceive().Track(scheduledOrder);
    }

    [Fact]
    public async Task Handle_CancelledSchedule_ReturnsNotActiveAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        scheduledOrder.Cancel();
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(Cmd(scheduledOrder.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_NOT_ACTIVE");
    }

    [Fact]
    public async Task Handle_FirstRunAtInPast_ReturnsValidationErrorAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId,
                IsAdmin: false,
                scheduledOrder.Id,
                RecurrenceType: null,
                FirstRunAt: DateTime.UtcNow.AddMinutes(-1),
                Notes: null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_FIRST_RUN_IN_PAST");
    }

    [Fact]
    public async Task Handle_InvalidRecurrenceType_ReturnsValidationErrorAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId,
                IsAdmin: false,
                scheduledOrder.Id,
                RecurrenceType: "monthly",
                FirstRunAt: null,
                Notes: null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesAndPersistsAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        var nextRun = DateTime.UtcNow.AddDays(3);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId,
                IsAdmin: false,
                scheduledOrder.Id,
                RecurrenceType: "weekly",
                FirstRunAt: nextRun,
                Notes: "new note"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecurrenceType.Should().Be("weekly");
        scheduledOrder.RecurrenceType.Should().Be(RecurrenceType.Weekly);
        scheduledOrder.FirstRunAt.Should().Be(nextRun);
        scheduledOrder.Notes.Should().Be("new note");
        _scheduledOrderRepository.Received(1).Track(scheduledOrder);
        await _scheduledOrderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminCanUpdateWithoutOwnershipLookupAsync()
    {
        var scheduledOrder = NewSchedule(OtherRestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId,
                IsAdmin: true,
                scheduledOrder.Id,
                RecurrenceType: "weekly",
                FirstRunAt: DateTime.UtcNow.AddDays(2),
                Notes: "admin edit"),
            default);

        result.IsSuccess.Should().BeTrue();
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ItemsProvided_ReplacesItemTemplateAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        scheduledOrder.AddItem(Guid.NewGuid(), 1);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId, IsAdmin: false, scheduledOrder.Id,
                RecurrenceType: null, FirstRunAt: null, Notes: null,
                DeliveryAddressId: null,
                Items: [new DraftOrderItemRequest(MarketProductId, 7)]),
            default);

        result.IsSuccess.Should().BeTrue();
        scheduledOrder.Items.Should().ContainSingle(i => i.MarketProductId == MarketProductId && i.Quantity == 7);
    }

    [Fact]
    public async Task Handle_DeliveryAddressIdProvided_ValidatesOwnershipAndReplacesAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId, IsAdmin: false, scheduledOrder.Id,
                RecurrenceType: null, FirstRunAt: null, Notes: null,
                DeliveryAddressId: DeliveryAddressId),
            default);

        result.IsSuccess.Should().BeTrue();
        scheduledOrder.DeliveryAddressId.Should().Be(DeliveryAddressId);
    }

    [Fact]
    public async Task Handle_DeliveryAddressIdNotOwnedByRestaurant_ReturnsNotFoundAsync()
    {
        var scheduledOrder = NewSchedule(RestaurantId);
        _scheduledOrderRepository.FindByIdAsync(scheduledOrder.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledOrder);
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.Handle(
            new UpdateScheduledOrderCommand(
                UserId, IsAdmin: false, scheduledOrder.Id,
                RecurrenceType: null, FirstRunAt: null, Notes: null,
                DeliveryAddressId: DeliveryAddressId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
        _scheduledOrderRepository.DidNotReceive().Track(scheduledOrder);
    }

    private static UpdateScheduledOrderCommand Cmd(Guid scheduledOrderId) =>
        new(UserId, IsAdmin: false, scheduledOrderId, "weekly", DateTime.UtcNow.AddDays(2), "updated");

    private static ScheduledOrder NewSchedule(Guid restaurantId) =>
        new(restaurantId, RecurrenceType.Daily, DateTime.UtcNow.AddDays(1), "note");
}
