using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.AssignRole;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AssignRoleCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly AssignRoleCommandHandler _sut;

    public AssignRoleCommandHandlerTests() =>
        _sut = new AssignRoleCommandHandler(_users, _roles, _tokens);

    [Fact]
    public async Task Handle_ValidAssignment_ChangesRoleRevokesTokensReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var driverRole = new Role("driver", "Delivery driver");
        var hubRole = new Role("hub_staff", "Hub staff");
        var user = User.Create("u@test.com", "hash", driverRole);

        _users.FindByIdAsync(userId, default).Returns(user);
        _roles.FindByNameAsync("hub_staff", default).Returns(hubRole);

        // Act
        var result = await _sut.Handle(new AssignRoleCommand(userId, "hub_staff"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("hub_staff");
        result.Value.Email.Should().Be("u@test.com");
        user.RoleId.Should().Be(hubRole.Id);
        await _tokens.Received(1).RevokeByUserAsync(userId, "ROLE_CHANGED", default);
        await _users.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        _users.FindByIdAsync(Arg.Any<Guid>(), default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new AssignRoleCommand(Guid.NewGuid(), "driver"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_InvalidRoleName_ReturnsInvalidRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("u@test.com", "hash", new Role("driver", "Driver"));

        _users.FindByIdAsync(userId, default).Returns(user);
        _roles.FindByNameAsync("nonexistent", default).Returns((Role?)null);

        // Act
        var result = await _sut.Handle(new AssignRoleCommand(userId, "nonexistent"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ROLE");
        await _users.DidNotReceive().SaveChangesAsync(default);
    }
}
