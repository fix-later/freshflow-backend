using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.CreateScheduledOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateScheduledOrderCommandHandlerTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly CreateScheduledOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid DeliveryAddressId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public CreateScheduledOrderCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryAddressSourceDto(
                DeliveryAddressId, "Bếp trưởng", "0901234567", "1 Test Street", 10.123456m, 106.123456m));
        _sut = new CreateScheduledOrderCommandHandler(_scheduledOrderRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(Cmd(firstRunAt: DateTime.UtcNow.AddDays(1)), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _scheduledOrderRepository.DidNotReceive().AddAsync(
            Arg.Any<ScheduledOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RestaurantNotApproved_ReturnsRestaurantNotApprovedAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: false));

        var result = await _sut.Handle(Cmd(firstRunAt: DateTime.UtcNow.AddDays(1)), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_APPROVED");
    }

    [Fact]
    public async Task Handle_FirstRunAtInPast_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(Cmd(firstRunAt: DateTime.UtcNow.AddMinutes(-1)), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCHEDULED_ORDER_FIRST_RUN_IN_PAST");
    }

    [Fact]
    public async Task Handle_InvalidRecurrenceType_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(
            new CreateScheduledOrderCommand(
                UserId,
                "monthly",
                DateTime.UtcNow.AddDays(1),
                "morning prep",
                DeliveryAddressId,
                [new DraftOrderItemRequest(MarketProductId, 3)]),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Handle_DeliveryAddressNotOwnedByRestaurant_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.Handle(Cmd(DateTime.UtcNow.AddDays(1)), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
        await _scheduledOrderRepository.DidNotReceive().AddAsync(
            Arg.Any<ScheduledOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsAndReturnsDtoAsync()
    {
        var firstRunAt = DateTime.UtcNow.AddDays(1);

        var result = await _sut.Handle(Cmd(firstRunAt), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.RecurrenceType.Should().Be("daily");
        result.Value.FirstRunAt.Should().Be(firstRunAt);
        result.Value.DeliveryAddressId.Should().Be(DeliveryAddressId);
        result.Value.Items.Should().ContainSingle(i => i.MarketProductId == MarketProductId && i.Quantity == 3);
        await _scheduledOrderRepository.Received(1).AddAsync(
            Arg.Is<ScheduledOrder>(s =>
                s.RestaurantId == RestaurantId
                && s.FirstRunAt == firstRunAt
                && s.Notes == "morning prep"
                && s.DeliveryAddressId == DeliveryAddressId
                && s.Items.Count == 1),
            Arg.Any<CancellationToken>());
        await _scheduledOrderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CreateScheduledOrderCommand Cmd(DateTime firstRunAt) =>
        new(UserId, "daily", firstRunAt, "morning prep", DeliveryAddressId, [new DraftOrderItemRequest(MarketProductId, 3)]);
}
