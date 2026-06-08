using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AddDeliveryAddressCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly IDeliveryAddressRepository _addresses = Substitute.For<IDeliveryAddressRepository>();
    private readonly AddDeliveryAddressCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public AddDeliveryAddressCommandHandlerTests() =>
        _sut = new AddDeliveryAddressCommandHandler(_restaurants, _addresses);

    private static RestaurantDto RestaurantFor(Guid userId) =>
        new(RestaurantId, "Test Restaurant", true, DateTime.UtcNow, userId);

    private static DeliveryAddressDto SampleAddress(Guid id) =>
        new(id, RestaurantId, "John", "+84901234567", "123 Main St",
            10.762622m, 106.660172m, false, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Handle_ValidCommand_AddsAndReturnsAddress()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.AddAsync(
            RestaurantId, "John", "+84901234567", "123 Main St",
            10.762622m, 106.660172m, false, default)
            .Returns(SampleAddress(addressId));

        var command = new AddDeliveryAddressCommand(
            UserId, "John", "+84901234567", "123 Main St",
            10.762622m, 106.660172m, false);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(addressId);
        result.Value.AddressLine.Should().Be("123 Main St");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns((RestaurantDto?)null);

        var command = new AddDeliveryAddressCommand(
            UserId, null, null, "Some address", null, null, false);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_IsDefaultTrue_ClearsExistingDefaultsFirst()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        var defaultAddress = SampleAddress(addressId) with { IsDefault = true };
        _addresses.AddAsync(
            RestaurantId, null, null, "New default address",
            null, null, true, default)
            .Returns(defaultAddress);

        var command = new AddDeliveryAddressCommand(
            UserId, null, null, "New default address", null, null, true);

        // Act
        await _sut.Handle(command, default);

        // Assert
        await _addresses.Received(1).ClearDefaultsAsync(RestaurantId, default);
    }

    [Fact]
    public async Task Handle_IsDefaultFalse_DoesNotClearDefaults()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.AddAsync(
            RestaurantId, null, null, "Regular address",
            null, null, false, default)
            .Returns(SampleAddress(addressId));

        var command = new AddDeliveryAddressCommand(
            UserId, null, null, "Regular address", null, null, false);

        // Act
        await _sut.Handle(command, default);

        // Assert
        await _addresses.DidNotReceive().ClearDefaultsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
