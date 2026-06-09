using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.UpdateMyProfile;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateMyProfileCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly UpdateMyProfileCommandHandler _sut;

    public UpdateMyProfileCommandHandlerTests() =>
        _sut = new UpdateMyProfileCommandHandler(_users);

    private static User AnyUser(string? phone = null) =>
        User.Create("owner@test.com", "hashed", new Role("restaurant", "Restaurant"), phone);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesAndReturnsProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = AnyUser();
        _users.FindByIdAsync(userId, default).Returns(user);
        _users.ExistsByPhoneAsync(Arg.Any<string>(), default).Returns(false);

        var command = new UpdateMyProfileCommand(
            userId, "Nguyen Van A", "+84901234567", "https://example.com/avatar.jpg");

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Nguyen Van A");
        result.Value.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        await _users.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _users.FindByIdAsync(Arg.Any<Guid>(), default).Returns((User?)null);

        var command = new UpdateMyProfileCommand(Guid.NewGuid(), "Name", null, null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
        await _users.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PhoneConflict_ReturnsConflictError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = AnyUser(phone: null);
        _users.FindByIdAsync(userId, default).Returns(user);
        _users.ExistsByPhoneAsync("+84901234567", default).Returns(true);

        var command = new UpdateMyProfileCommand(userId, null, "+84901234567", null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PHONE_ALREADY_EXISTS");
        await _users.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PhoneUnchanged_SkipsUniquenessCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        // Phone stored normalised: trimmed + lowercase
        var user = AnyUser(phone: "+84901234567");
        _users.FindByIdAsync(userId, default).Returns(user);

        // Send the same phone value — handler must not call ExistsByPhoneAsync
        var command = new UpdateMyProfileCommand(userId, "Updated Name", "+84901234567", null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _users.DidNotReceive().ExistsByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullPhone_ClearsPhone()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = AnyUser(phone: "+84901234567");
        _users.FindByIdAsync(userId, default).Returns(user);

        var command = new UpdateMyProfileCommand(userId, "Name", null, null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NullAllOptionals_UpdatesWithNulls()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = AnyUser();
        _users.FindByIdAsync(userId, default).Returns(user);

        var command = new UpdateMyProfileCommand(userId, null, null, null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().BeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.AvatarUrl.Should().BeNull();
    }
}
