using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Delete;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeleteDeliveryAddressCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly IDeliveryAddressRepository _addresses = Substitute.For<IDeliveryAddressRepository>();
    private readonly DeleteDeliveryAddressCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid AddressId = Guid.NewGuid();

    public DeleteDeliveryAddressCommandHandlerTests() =>
        _sut = new DeleteDeliveryAddressCommandHandler(_restaurants, _addresses);

    private static RestaurantDto RestaurantFor(Guid userId) =>
        new(RestaurantId, "Test", RestaurantStatus.Active, DateTime.UtcNow, userId);

    private static DeliveryAddressDto SampleAddress() =>
        new(AddressId, RestaurantId, null, null, "123 Street",
            null, null, false, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesAddress()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default).Returns(SampleAddress());

        var command = new DeleteDeliveryAddressCommand(UserId, AddressId);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _addresses.Received(1).SoftDeleteAsync(AddressId, RestaurantId, default);
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns((RestaurantDto?)null);

        var command = new DeleteDeliveryAddressCommand(UserId, AddressId);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
        await _addresses.DidNotReceive().SoftDeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AddressNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.FindByIdAndRestaurantIdAsync(AddressId, RestaurantId, default)
            .Returns((DeliveryAddressDto?)null);

        var command = new DeleteDeliveryAddressCommand(UserId, AddressId);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
        await _addresses.DidNotReceive().SoftDeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
