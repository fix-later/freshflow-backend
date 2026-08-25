using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.UpdateDraftOrder;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateDraftOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IOperationalSettingsRepository _operationalSettings = Substitute.For<IOperationalSettingsRepository>();

    private readonly UpdateDraftOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public UpdateDraftOrderCommandHandlerTests()
    {
        _sut = new UpdateDraftOrderCommandHandler(_orderRepository, _restaurantReader, _operationalSettings);

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(OperationalSettings.CreateDefault());
    }

    private static Order NewDraftOrder(Guid? restaurantId = null, string? notes = "old notes") =>
        new(restaurantId ?? RestaurantId, scheduledFor: null, notes: notes);

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(
            new UpdateDraftOrderCommand(UserId, Guid.NewGuid(), "new notes", null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderBelongsToAnotherRestaurant_ReturnsForbiddenAsync()
    {
        // Arrange
        var order = NewDraftOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(
            new UpdateDraftOrderCommand(UserId, order.Id, "new notes", null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ScheduledForBeyondWindow_ReturnsDeliveryDateOutOfWindowAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(
            new UpdateDraftOrderCommand(UserId, order.Id, "new notes", DateTime.UtcNow.AddDays(8)), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
    }

    [Fact]
    public async Task Handle_OrderNotDraft_ReturnsOrderNotDraftErrorAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        order.AddItem(Guid.NewGuid(), "Cà chua", 5, 20_000m);
        order.Confirm();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(
            new UpdateDraftOrderCommand(UserId, order.Id, "new notes", null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public async Task Handle_Success_UpdatesNotesAndScheduledForAndPersistsAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var newDate = DateTime.UtcNow.AddDays(2);

        // Act
        var result = await _sut.Handle(
            new UpdateDraftOrderCommand(UserId, order.Id, "new notes", newDate), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Notes.Should().Be("new notes");
        result.Value.ScheduledFor.Should().Be(newDate);
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
