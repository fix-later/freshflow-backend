using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Login;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
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
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _tokenService.AccessTokenTtlSeconds.Returns(900);
        _tokenService.RefreshTokenTtlDays.Returns(7);
        _sut = new LoginCommandHandler(_users, _tokens, _hasher, _tokenService, _restaurants);
    }

    [Fact]
    public async Task Handle_ValidEmailCredentials_ReturnsTokens()
    {
        var user = User.Create("admin@test.com", "hashed", new Role("admin", "Admin"), fullName: "Admin User");
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
        result.Value.User.FullName.Should().Be("Admin User");
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
        result.Value.User.FullName.Should().BeNull();
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

    [Fact]
    public async Task Handle_LockedAccount_ReturnsAccountLocked()
    {
        // Arrange — user already locked (5 prior failed attempts)
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();
        _users.FindByIdentifierAsync("u@test.com", default).Returns(user);

        // Act
        var result = await _sut.Handle(new LoginCommand("u@test.com", "anything"), default);

        // Assert — no password check, no counter increment
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNT_LOCKED");
        await _users.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsFailedLoginCount()
    {
        // Arrange
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        _users.FindByIdentifierAsync("u@test.com", default).Returns(user);
        _hasher.Verify("wrong", "hashed").Returns(false);

        // Act
        var result = await _sut.Handle(new LoginCommand("u@test.com", "wrong"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
        user.FailedLoginCount.Should().Be(1);
        await _users.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_FifthWrongPassword_LocksAccount()
    {
        // Arrange — 4 previous failures
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        for (var i = 0; i < 4; i++) user.RecordFailedLogin();
        _users.FindByIdentifierAsync("u@test.com", default).Returns(user);
        _hasher.Verify("wrong", "hashed").Returns(false);

        // Act — 5th bad attempt
        var result = await _sut.Handle(new LoginCommand("u@test.com", "wrong"), default);

        // Assert — still INVALID_CREDENTIALS but account is now locked for next attempt
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
        user.IsLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ClearsFailedLoginCount()
    {
        // Arrange — user has 3 previous failures
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));
        for (var i = 0; i < 3; i++) user.RecordFailedLogin();
        _users.FindByIdentifierAsync("u@test.com", default).Returns(user);
        _hasher.Verify("correct", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user.Id, user.Email, Arg.Any<string>()).Returns("access");
        _tokenService.GenerateRefreshToken().Returns("raw");
        _tokenService.HashRefreshToken("raw").Returns("hashed-refresh");

        // Act
        var result = await _sut.Handle(new LoginCommand("u@test.com", "correct"), default);

        // Assert — counter reset on success
        result.IsSuccess.Should().BeTrue();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    // H4 — Restaurant role returns ApprovalStatus
    [Fact]
    public async Task Handle_RestaurantRole_PopulatesApprovalStatus()
    {
        // Arrange
        var restaurantRole = new Role(RoleNames.Restaurant, "Restaurant");
        var user = User.Create("resto@test.com", "hashed", restaurantRole);
        _users.FindByIdentifierAsync("resto@test.com", default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user.Id, user.Email, Arg.Any<string>()).Returns("access");
        _tokenService.GenerateRefreshToken().Returns("raw");
        _tokenService.HashRefreshToken("raw").Returns("hashed-refresh");

        var restaurantDto = new RestaurantDto(
            Guid.NewGuid(), "My Restaurant", RestaurantStatus.Pending,
            DateTime.UtcNow, user.Id);
        _restaurants.FindByUserIdAsync(user.Id, default).Returns(restaurantDto);

        // Act
        var result = await _sut.Handle(new LoginCommand("resto@test.com", "P@ss1"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ApprovalStatus.Should().Be(RestaurantStatus.Pending);
        result.Value.User.Role.Should().Be(RoleNames.Restaurant);
    }

    [Fact]
    public async Task Handle_RestaurantRole_ActiveRestaurant_PopulatesActiveStatus()
    {
        // Arrange
        var restaurantRole = new Role(RoleNames.Restaurant, "Restaurant");
        var user = User.Create("active_resto@test.com", "hashed", restaurantRole);
        _users.FindByIdentifierAsync("active_resto@test.com", default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user.Id, user.Email, Arg.Any<string>()).Returns("access");
        _tokenService.GenerateRefreshToken().Returns("raw");
        _tokenService.HashRefreshToken("raw").Returns("hashed-refresh");

        var restaurantDto = new RestaurantDto(
            Guid.NewGuid(), "Active Restaurant", RestaurantStatus.Active,
            DateTime.UtcNow, user.Id);
        _restaurants.FindByUserIdAsync(user.Id, default).Returns(restaurantDto);

        // Act
        var result = await _sut.Handle(new LoginCommand("active_resto@test.com", "P@ss1"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ApprovalStatus.Should().Be(RestaurantStatus.Active);
    }

    [Fact]
    public async Task Handle_NonRestaurantRole_ApprovalStatusIsNull()
    {
        // Arrange
        var driverRole = new Role("driver", "Driver");
        var user = User.Create("driver@test.com", "hashed", driverRole);
        _users.FindByIdentifierAsync("driver@test.com", default).Returns(user);
        _hasher.Verify("P@ss1", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user.Id, user.Email, Arg.Any<string>()).Returns("access");
        _tokenService.GenerateRefreshToken().Returns("raw");
        _tokenService.HashRefreshToken("raw").Returns("hashed-refresh");

        // Act
        var result = await _sut.Handle(new LoginCommand("driver@test.com", "P@ss1"), default);

        // Assert — no restaurant repo call, no ApprovalStatus
        result.IsSuccess.Should().BeTrue();
        result.Value.ApprovalStatus.Should().BeNull();
        await _restaurants.DidNotReceive().FindByUserIdAsync(Arg.Any<Guid>(), default);
    }

    // M9 — Timing attack: dummy BCrypt verify when user not found
    [Fact]
    public async Task Handle_IdentifierNotFound_StillRunsDummyVerify()
    {
        // Arrange
        _users.FindByIdentifierAsync(Arg.Any<string>(), default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new LoginCommand("no@one.com", "pass"), default);

        // Assert — INVALID_CREDENTIALS returned AND hasher was still called
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
        _hasher.Received(1).Verify("pass", Arg.Any<string>());
    }
}
