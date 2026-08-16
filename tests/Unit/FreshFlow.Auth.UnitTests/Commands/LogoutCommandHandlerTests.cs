using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Logout;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly LogoutCommandHandler _sut;

    public LogoutCommandHandlerTests() =>
        _sut = new LogoutCommandHandler(_tokens, _tokenService);

    [Fact]
    public async Task Handle_ValidToken_RevokesToken_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = new RefreshToken(userId, "hashed", Guid.NewGuid(), 7);
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns(token);

        // Act
        var result = await _sut.Handle(new LogoutCommand(userId, "raw"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
        token.RevokedReason.Should().Be("logout");
        await _tokens.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_TokenAlreadyRevoked_ReturnsSuccess_Idempotent()
    {
        // Arrange — already-revoked token belonging to the same user must return 204 (idempotent).
        var userId = Guid.NewGuid();
        var token = new RefreshToken(userId, "hashed", Guid.NewGuid(), 7);
        token.Revoke(reason: "logout");
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns(token);

        // Act
        var result = await _sut.Handle(new LogoutCommand(userId, "raw"), default);

        // Assert — idempotent success; no double-save
        result.IsSuccess.Should().BeTrue();
        await _tokens.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns((RefreshToken?)null);

        // Act
        var result = await _sut.Handle(new LogoutCommand(Guid.NewGuid(), "raw"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("TOKEN_NOT_FOUND");
        await _tokens.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_TokenBelongsToDifferentUser_ReturnsNotFoundError()
    {
        // Arrange — ownership guard: caller must own the token they are revoking.
        var ownerUserId = Guid.NewGuid();
        var callerUserId = Guid.NewGuid(); // different user
        var token = new RefreshToken(ownerUserId, "hashed", Guid.NewGuid(), 7);
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns(token);

        // Act
        var result = await _sut.Handle(new LogoutCommand(callerUserId, "raw"), default);

        // Assert — same error code as not-found to avoid oracle leak
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("TOKEN_NOT_FOUND");
        await _tokens.DidNotReceive().SaveChangesAsync(default);
    }
}
