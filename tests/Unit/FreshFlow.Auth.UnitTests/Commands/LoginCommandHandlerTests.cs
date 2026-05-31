using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Login;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class LoginCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _tokenService.AccessTokenTtlSeconds.Returns(900);
        _tokenService.RefreshTokenTtlDays.Returns(7);
        _sut = new LoginCommandHandler(_users, _tokens, _hasher, _tokenService);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        var user = User.Create("admin@test.com", "hashed", UserRole.Admin);
        _users.FindByEmailAsync("admin@test.com", default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user.Id, user.Email, Arg.Any<string>()).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("raw-refresh");
        _tokenService.HashRefreshToken("raw-refresh").Returns("hashed-refresh");

        var result = await _sut.Handle(new LoginCommand("admin@test.com", "P@ss1"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("raw-refresh");
        result.Value.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCredentials()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), default).Returns((User?)null);

        var result = await _sut.Handle(new LoginCommand("no@one.com", "pass"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = User.Create("u@test.com", "hashed", UserRole.Driver);
        _users.FindByEmailAsync("u@test.com", default).Returns(user);
        _hasher.Verify("wrong", "hashed").Returns(false);

        var result = await _sut.Handle(new LoginCommand("u@test.com", "wrong"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsAccountInactive()
    {
        var user = User.Create("u@test.com", "hashed", UserRole.HubStaff);
        user.Deactivate();
        _users.FindByEmailAsync("u@test.com", default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);

        var result = await _sut.Handle(new LoginCommand("u@test.com", "P@ss1"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNT_INACTIVE");
    }
}
