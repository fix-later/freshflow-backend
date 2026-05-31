using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Logout;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Enums;
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
        var userId = Guid.NewGuid();
        var token = new RefreshToken(userId, "hashed", Guid.NewGuid(), 7);
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns(token);

        var result = await _sut.Handle(new LogoutCommand(userId, "raw"), default);

        result.IsSuccess.Should().BeTrue();
        await _tokens.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_TokenAlreadyRevoked_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var token = new RefreshToken(userId, "hashed", Guid.NewGuid(), 7);
        token.Revoke();
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns(token);

        var result = await _sut.Handle(new LogoutCommand(userId, "raw"), default);

        // Silently succeeds — prevents oracle attack on token existence
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsSuccess()
    {
        _tokenService.HashRefreshToken("raw").Returns("hashed");
        _tokens.FindByHashAsync("hashed", default).Returns((RefreshToken?)null);

        var result = await _sut.Handle(new LogoutCommand(Guid.NewGuid(), "raw"), default);

        result.IsSuccess.Should().BeTrue();
    }
}
