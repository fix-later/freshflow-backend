using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.UnlockUser;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UnlockUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly UnlockUserCommandHandler _sut;

    public UnlockUserCommandHandlerTests() =>
        _sut = new UnlockUserCommandHandler(_users);

    [Fact]
    public async Task Handle_LockedUser_UnlocksAndReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("u@test.com", "hash", new Role("driver", "Driver"));
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();
        _users.FindByIdAsync(userId, default).Returns(user);

        // Act
        var result = await _sut.Handle(new UnlockUserCommand(userId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsLockedOut.Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        await _users.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        _users.FindByIdAsync(Arg.Any<Guid>(), default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new UnlockUserCommand(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
        await _users.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_AlreadyUnlockedUser_IsIdempotent()
    {
        // Arrange — user has no lockout state
        var userId = Guid.NewGuid();
        var user = User.Create("u@test.com", "hash", new Role("driver", "Driver"));
        _users.FindByIdAsync(userId, default).Returns(user);

        // Act
        var result = await _sut.Handle(new UnlockUserCommand(userId), default);

        // Assert — still succeeds idempotently
        result.IsSuccess.Should().BeTrue();
        user.FailedLoginCount.Should().Be(0);
        await _users.Received(1).SaveChangesAsync(default);
    }
}
