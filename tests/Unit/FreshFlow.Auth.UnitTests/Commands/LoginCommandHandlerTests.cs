using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Login;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
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
    public async Task Handle_ValidEmailCredentials_ReturnsTokens()
    {
        var user = User.Create("admin@test.com", "hashed", new Role("admin", "Admin"));
        _users.FindByIdentifierAsync("admin@test.com", default).Returns(user);
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
    public async Task Handle_ValidPhoneCredentials_ReturnsTokens()
    {
        const string phone = "+84901234567";
        var user = User.Create("driver@test.com", "hashed", new Role("driver", "Driver"), phone);
        _users.FindByIdentifierAsync(phone, default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user.Id, user.Email, Arg.Any<string>()).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("raw-refresh");
        _tokenService.HashRefreshToken("raw-refresh").Returns("hashed-refresh");

        var result = await _sut.Handle(new LoginCommand(phone, "P@ss1"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        // LoginUserDto.Email comes from the resolved user's email, not the phone identifier
        result.Value.User.Email.Should().Be("driver@test.com");
    }

    [Fact]
    public async Task Handle_IdentifierNotFound_ReturnsInvalidCredentials()
    {
        _users.FindByIdentifierAsync(Arg.Any<string>(), default).Returns((User?)null);

        var result = await _sut.Handle(new LoginCommand("no@one.com", "pass"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        _users.FindByIdentifierAsync("u@test.com", default).Returns(user);
        _hasher.Verify("wrong", "hashed").Returns(false);

        var result = await _sut.Handle(new LoginCommand("u@test.com", "wrong"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsAccountInactive()
    {
        var user = User.Create("u@test.com", "hashed", new Role("hub_staff", "Hub Staff"));
        user.Deactivate();
        _users.FindByIdentifierAsync("u@test.com", default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);

        var result = await _sut.Handle(new LoginCommand("u@test.com", "P@ss1"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNT_INACTIVE");
    }
}
