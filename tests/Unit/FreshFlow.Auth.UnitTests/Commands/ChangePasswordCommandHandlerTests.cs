using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.ChangePassword;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ChangePasswordCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ChangePasswordCommandHandler _sut;

    public ChangePasswordCommandHandlerTests() =>
        _sut = new ChangePasswordCommandHandler(_users, _tokens, _hasher);

    [Fact]
    public async Task Handle_ValidCurrentPassword_ChangesPasswordAndRevokesUserTokens()
    {
        var user = User.Create("user@test.com", "old-hash", new Role("driver", "Driver"));
        _users.FindByIdAsync(user.Id, default).Returns(user);
        _hasher.Verify("OldP@ss1", "old-hash").Returns(true);
        _hasher.Hash("NewP@ss1").Returns("new-hash");

        var result = await _sut.Handle(
            new ChangePasswordCommand(user.Id, "OldP@ss1", "NewP@ss1"), default);

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new-hash");
        await _users.Received(1).SaveChangesAsync(default);
        await _tokens.Received(1).RevokeByUserAsync(user.Id, Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ReturnsInvalidCurrentPassword()
    {
        var user = User.Create("user@test.com", "old-hash", new Role("driver", "Driver"));
        _users.FindByIdAsync(user.Id, default).Returns(user);
        _hasher.Verify("WrongP@ss1", "old-hash").Returns(false);

        var result = await _sut.Handle(
            new ChangePasswordCommand(user.Id, "WrongP@ss1", "NewP@ss1"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CURRENT_PASSWORD");
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
        await _tokens.DidNotReceive().RevokeByUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), default);
        await _users.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsUnauthorized()
    {
        var userId = Guid.NewGuid();
        _users.FindByIdAsync(userId, default).Returns((User?)null);

        var result = await _sut.Handle(
            new ChangePasswordCommand(userId, "OldP@ss1", "NewP@ss1"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsUnauthorized()
    {
        var user = User.Create("user@test.com", "old-hash", new Role("driver", "Driver"));
        user.Deactivate();
        _users.FindByIdAsync(user.Id, default).Returns(user);

        var result = await _sut.Handle(
            new ChangePasswordCommand(user.Id, "OldP@ss1", "NewP@ss1"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_SamePassword_ReturnsValidationError()
    {
        var user = User.Create("user@test.com", "old-hash", new Role("driver", "Driver"));
        _users.FindByIdAsync(user.Id, default).Returns(user);
        _hasher.Verify("SameP@ss1", "old-hash").Returns(true);

        var result = await _sut.Handle(
            new ChangePasswordCommand(user.Id, "SameP@ss1", "SameP@ss1"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _tokens.DidNotReceive().RevokeByUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), default);
    }
}
