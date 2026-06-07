using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.ActivateUser;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ActivateUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ActivateUserCommandHandler _sut;

    public ActivateUserCommandHandlerTests() =>
        _sut = new ActivateUserCommandHandler(_users);

    [Fact]
    public async Task Handle_DeactivateUser_SetsIsActiveFalse()
    {
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        _users.FindByIdAsync(userId, default).Returns(user);

        var result = await _sut.Handle(new ActivateUserCommand(userId, false, adminId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        _users.FindByIdAsync(Arg.Any<Guid>(), default).Returns((User?)null);

        var result = await _sut.Handle(
            new ActivateUserCommand(Guid.NewGuid(), false, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AdminDeactivatesSelf_Returns422()
    {
        var adminId = Guid.NewGuid();

        var result = await _sut.Handle(new ActivateUserCommand(adminId, false, adminId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CANNOT_DEACTIVATE_SELF");
    }

    [Fact]
    public async Task Handle_ReactivateUser_SetsIsActiveTrue()
    {
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        user.Deactivate();
        _users.FindByIdAsync(userId, default).Returns(user);

        var result = await _sut.Handle(new ActivateUserCommand(userId, true, adminId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
    }
}
