using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.UpdateOrderNotes;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateOrderNotesCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();

    private readonly UpdateOrderNotesCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public UpdateOrderNotesCommandHandlerTests()
    {
        _sut = new UpdateOrderNotesCommandHandler(_orderRepository, _restaurantReader);

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
    }

    private static Order NewDraftOrder(Guid? restaurantId = null, string? notes = "old notes") =>
        new(restaurantId ?? RestaurantId, scheduledFor: null, notes: notes);

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.Handle(new UpdateOrderNotesCommand(UserId, Guid.NewGuid(), "new notes"), default);

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
        var result = await _sut.Handle(new UpdateOrderNotesCommand(UserId, order.Id, "new notes"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
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
        var result = await _sut.Handle(new UpdateOrderNotesCommand(UserId, order.Id, "new notes"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public async Task Handle_Success_UpdatesNotesAndPersistsAsync()
    {
        // Arrange
        var order = NewDraftOrder();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await _sut.Handle(new UpdateOrderNotesCommand(UserId, order.Id, "new notes"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Notes.Should().Be("new notes");
        await _orderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
