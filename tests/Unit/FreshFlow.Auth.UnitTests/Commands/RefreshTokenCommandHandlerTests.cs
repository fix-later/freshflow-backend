using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.RefreshToken;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RefreshTokenCommandHandlerTests
{
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly RefreshTokenCommandHandler _sut;

    public RefreshTokenCommandHandlerTests()
    {
        _tokenService.AccessTokenTtlSeconds.Returns(900);
        _tokenService.RefreshTokenTtlDays.Returns(7);
        _sut = new RefreshTokenCommandHandler(_tokens, _users, _tokenService);
    }

    private RefreshToken MakeValidToken(Guid userId, string hash) =>
        new(userId, hash, Guid.NewGuid(), 7);

    [Fact]
    public async Task Handle_ValidToken_RotatesAndReturnsNewPair()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string raw = "raw-token";
        const string hash = "sha256-hash";
        var stored = MakeValidToken(userId, hash);
        var user = User.Create("u@test.com", "hashed", new Role("driver", "Driver"));

        _tokenService.HashRefreshToken(raw).Returns(hash);
        _tokens.FindByHashAsync(hash, default).Returns(stored);
        _users.FindByIdAsync(userId, default).Returns(user);
        _tokenService.GenerateAccessToken(Arg.Any<Guid>(), user.Email, Arg.Any<string>()).Returns("new-access");
        _tokenService.GenerateRefreshToken().Returns("new-raw");
        _tokenService.HashRefreshToken("new-raw").Returns("new-hash");

        // Act
        var result = await _sut.Handle(new RefreshTokenCommand(raw), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        result.Value.RefreshToken.Should().Be("new-raw");
        stored.IsRevoked.Should().BeTrue();
        stored.RevokedReason.Should().Be("rotated");
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsTokenInvalid()
    {
        // Arrange
        _tokenService.HashRefreshToken(Arg.Any<string>()).Returns("hash");
        _tokens.FindByHashAsync("hash", default).Returns((RefreshToken?)null);

        // Act
        var result = await _sut.Handle(new RefreshTokenCommand("raw"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TOKEN_INVALID");
    }

    [Fact]
    public async Task Handle_RevokedToken_RevokesFamily_ReturnsRefreshTokenReuse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string hash = "hashed";
        var stored = MakeValidToken(userId, hash);
        stored.Revoke(reason: "logout"); // already revoked — simulates reuse

        _tokenService.HashRefreshToken("raw").Returns(hash);
        _tokens.FindByHashAsync(hash, default).Returns(stored);

        // Act
        var result = await _sut.Handle(new RefreshTokenCommand("raw"), default);

        // Assert — FR-AUTH-007 AC2: reuse → REFRESH_TOKEN_REUSE (HTTP 409)
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFRESH_TOKEN_REUSE");
        await _tokens.Received(1).RevokeByFamilyAsync(stored.FamilyId, "family_compromised", default);
        await _tokens.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsRefreshTokenExpired()
    {
        // Arrange — ttlDays: -1 creates an already-expired token
        var stored = new RefreshToken(Guid.NewGuid(), "hash", Guid.NewGuid(), ttlDays: -1);
        _tokenService.HashRefreshToken("raw").Returns("hash");
        _tokens.FindByHashAsync("hash", default).Returns(stored);

        // Act
        var result = await _sut.Handle(new RefreshTokenCommand("raw"), default);

        // Assert — FR-AUTH-007 AC2: expired → REFRESH_TOKEN_EXPIRED (HTTP 401)
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFRESH_TOKEN_EXPIRED");
    }

    [Fact]
    public async Task Handle_LockedOutUser_CanStillRefreshValidToken()
    {
        // Arrange — user is locked out (e.g. attacker hammering login endpoint)
        // but holds a valid refresh token issued before the lockout.
        var userId = Guid.NewGuid();
        const string raw = "raw-token";
        const string hash = "sha256-hash";
        var stored = MakeValidToken(userId, hash);

        var user = User.Create("victim@test.com", "hashed", new Role("restaurant", "Restaurant"));
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();   // locked out
        user.IsLockedOut.Should().BeTrue();

        _tokenService.HashRefreshToken(raw).Returns(hash);
        _tokens.FindByHashAsync(hash, default).Returns(stored);
        _users.FindByIdAsync(userId, default).Returns(user);
        _tokenService.GenerateAccessToken(Arg.Any<Guid>(), user.Email, Arg.Any<string>()).Returns("new-access");
        _tokenService.GenerateRefreshToken().Returns("new-raw");
        _tokenService.HashRefreshToken("new-raw").Returns("new-hash");

        // Act
        var result = await _sut.Handle(new RefreshTokenCommand(raw), default);

        // Assert — lockout must NOT kill refresh sessions (DoS prevention)
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
    }
}
