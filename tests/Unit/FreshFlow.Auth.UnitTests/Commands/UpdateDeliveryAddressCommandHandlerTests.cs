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
            AddressId, "Jane", null, "New line", null, null, false, default)
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
    public async Task Handle_IsDefaultTrue_ClearsExistingDefaultsFirst()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());
        _addresses.UpdateAsync(
            AddressId, null, null, "Updated", null, null, true, default)
            .Returns(SampleAddress(isDefault: true) with { AddressLine = "Updated" });

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Updated", null, null, true);

        // Act
        await _sut.Handle(command, default);

        // Assert
        await _addresses.Received(1).ClearDefaultsAsync(RestaurantId, default);
    }

    [Fact]
    public async Task Handle_IsDefaultFalse_DoesNotClearDefaults()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());
        _addresses.UpdateAsync(
            AddressId, null, null, "Updated", null, null, false, default)
            .Returns(SampleAddress() with { AddressLine = "Updated" });

        var command = new UpdateDeliveryAddressCommand(
            UserId, AddressId, null, null, "Updated", null, null, false);

        // Act
        await _sut.Handle(command, default);

        // Assert
        await _addresses.DidNotReceive().ClearDefaultsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
