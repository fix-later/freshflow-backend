using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateRestaurantProfileCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly UpdateRestaurantProfileCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public UpdateRestaurantProfileCommandHandlerTests() =>
        _sut = new UpdateRestaurantProfileCommandHandler(_restaurants);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsUpdatedProfile()
    {
        // Arrange
        var existingDto = new RestaurantDto(
            RestaurantId, "Old Name", RestaurantStatus.Active, DateTime.UtcNow, UserId);

        var updatedDto = new RestaurantDto(
            RestaurantId, "New Name", RestaurantStatus.Active, DateTime.UtcNow, UserId,
            "123 Main St", "John Doe",
            new TimeOnly(8, 0), new TimeOnly(12, 0));

        _restaurants.FindByUserIdAsync(UserId, default).Returns(existingDto);
        _restaurants.UpdateProfileAsync(
            RestaurantId, "New Name", "123 Main St", "John Doe",
            new TimeOnly(8, 0), new TimeOnly(12, 0), default)
            .Returns(updatedDto);

        var command = new UpdateRestaurantProfileCommand(
            UserId, "New Name", "123 Main St", "John Doe",
            new TimeOnly(8, 0), new TimeOnly(12, 0));

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.Name.Should().Be("New Name");
        result.Value.Address.Should().Be("123 Main St");
        result.Value.ContactPerson.Should().Be("John Doe");
        result.Value.PickupStart.Should().Be(new TimeOnly(8, 0));
        result.Value.PickupEnd.Should().Be(new TimeOnly(12, 0));
    }

    [Fact]
    public async Task Handle_MinimalCommand_OnlyName_Succeeds()
    {
        // Arrange
        var existingDto = new RestaurantDto(
            RestaurantId, "Old Name", RestaurantStatus.Pending, DateTime.UtcNow, UserId);

        var updatedDto = new RestaurantDto(
            RestaurantId, "Updated Name", RestaurantStatus.Pending, DateTime.UtcNow, UserId);

        _restaurants.FindByUserIdAsync(UserId, default).Returns(existingDto);
        _restaurants.UpdateProfileAsync(
            RestaurantId, "Updated Name", null, null, null, null, default)
            .Returns(updatedDto);

        var command = new UpdateRestaurantProfileCommand(
            UserId, "Updated Name", null, null, null, null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Name");
        result.Value.Address.Should().BeNull();
        result.Value.ContactPerson.Should().BeNull();
        result.Value.PickupStart.Should().BeNull();
        result.Value.PickupEnd.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns((RestaurantDto?)null);

        var command = new UpdateRestaurantProfileCommand(
            UserId, "Some Name", null, null, null, null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsUpdateWithCorrectArguments()
    {
        // Arrange
        var existingDto = new RestaurantDto(
            RestaurantId, "Old", RestaurantStatus.Active, DateTime.UtcNow, UserId);

        var updatedDto = new RestaurantDto(
            RestaurantId, "New", RestaurantStatus.Active, DateTime.UtcNow, UserId,
            "456 Street", null, new TimeOnly(9, 30), new TimeOnly(17, 0));

        _restaurants.FindByUserIdAsync(UserId, default).Returns(existingDto);
        _restaurants.UpdateProfileAsync(
            RestaurantId, "New", "456 Street", null,
            new TimeOnly(9, 30), new TimeOnly(17, 0), default)
            .Returns(updatedDto);

        var command = new UpdateRestaurantProfileCommand(
            UserId, "New", "456 Street", null,
            new TimeOnly(9, 30), new TimeOnly(17, 0));

        // Act
        await _sut.Handle(command, default);

        // Assert
        await _restaurants.Received(1).UpdateProfileAsync(
            RestaurantId, "New", "456 Street", null,
            new TimeOnly(9, 30), new TimeOnly(17, 0), default);
    }
}
