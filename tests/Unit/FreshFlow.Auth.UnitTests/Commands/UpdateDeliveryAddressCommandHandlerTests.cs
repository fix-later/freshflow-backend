using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Update;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateDeliveryAddressCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly IDeliveryAddressRepository _addresses = Substitute.For<IDeliveryAddressRepository>();
    private readonly UpdateDeliveryAddressCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid AddressId = Guid.NewGuid();

    public UpdateDeliveryAddressCommandHandlerTests() =>
        _sut = new UpdateDeliveryAddressCommandHandler(_restaurants, _addresses);

    private static RestaurantDto RestaurantFor(Guid userId) =>
        new(RestaurantId, "Test", RestaurantStatus.Active, DateTime.UtcNow, userId);

    private static DeliveryAddressDto SampleAddress(bool isDefault = false) =>
        new(AddressId, RestaurantId, "John", null, "Old line",
            null, null, isDefault, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesAndReturnsAddress()
    {
        // Arrange
        var updated = SampleAddress() with { AddressLine = "New line" };
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());
        _addresses.UpdateAsync(
            AddressId, RestaurantId, "Jane", null, "New line", null, null, false, default)
            .Returns(updated);

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, "Jane", null, "New line", null, null, false);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AddressLine.Should().Be("New line");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns((RestaurantDto?)null);

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Some address", null, null, false);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AddressNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default)
            .Returns((DeliveryAddressDto?)null);

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Some address", null, null, false);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UpdateAsyncReturnsNull_ReturnsNotFoundError()
    {
        // Arrange — simulates race-condition where address disappears between Find and Update
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());
        _addresses.UpdateAsync(
            AddressId, RestaurantId, null, null, "Updated", null, null, false, default)
            .Returns((DeliveryAddressDto?)null);

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Updated", null, null, false);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_IsDefaultTrue_DelegatesAtomicDefaultClearToRepository()
    {
        // Arrange — atomicity (ClearDefaults + Update) is now handled inside UpdateAsync; handler must NOT call ClearDefaultsAsync
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());
        _addresses.UpdateAsync(
            AddressId, RestaurantId, null, null, "Updated", null, null, true, default)
            .Returns(SampleAddress(isDefault: true) with { AddressLine = "Updated" });

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Updated", null, null, true);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _addresses.DidNotReceive().ClearDefaultsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _addresses.Received(1).UpdateAsync(AddressId, RestaurantId, null, null, "Updated", null, null, true, default);
    }

    [Fact]
    public async Task Handle_IsDefaultFalse_DoesNotClearDefaults()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());
        _addresses.UpdateAsync(
            AddressId, RestaurantId, null, null, "Updated", null, null, false, default)
            .Returns(SampleAddress() with { AddressLine = "Updated" });

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Updated", null, null, false);

        // Act
        await _sut.Handle(command, default);

        // Assert
        await _addresses.DidNotReceive().ClearDefaultsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IsDefaultTrue_PropagatesIsDefaultTrueFromDto()
    {
        // Arrange — verifies handler correctly propagates IsDefault=true from the DTO returned by UpdateAsync.
        // The real regression guard for the EF identity-map clobber is the excludeId filter in
        // ClearDefaultsInternalAsync (infrastructure layer), documented there with an explanatory comment.
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default)
            .Returns(SampleAddress(isDefault: true));
        _addresses.UpdateAsync(
            AddressId, RestaurantId, null, null, "Updated", null, null, true, default)
            .Returns(SampleAddress(isDefault: true) with { AddressLine = "Updated" });

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Updated", null, null, true);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();
        result.Value.AddressLine.Should().Be("Updated");
    }
}
