using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.RefreshToken;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Enums;
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

    private RefreshToken MakeValidToken(Guid userId, string hash)
    {
        var familyId = Guid.NewGuid();
        return new RefreshToken(userId, hash, familyId, 7);
    }

    [Fact]
    public async Task Handle_ValidToken_RotatesAndReturnsNewPair()
    {
        var userId = Guid.NewGuid();
        const string raw = "raw-token";
        const string hash = "sha256-hash";
        var stored = MakeValidToken(userId, hash);
        var user = User.Create("u@test.com", "hashed", UserRole.Driver);

        _tokenService.HashRefreshToken(raw).Returns(hash);
        _tokens.FindByHashAsync(hash, default).Returns(stored);
        _users.FindByIdAsync(userId, default).Returns(user);
        _tokenService.GenerateAccessToken(Arg.Any<Guid>(), user.Email, Arg.Any<string>()).Returns("new-access");
        _tokenService.GenerateRefreshToken().Returns("new-raw");
        _tokenService.HashRefreshToken("new-raw").Returns("new-hash");

        var result = await _sut.Handle(new RefreshTokenCommand(raw), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        result.Value.RefreshToken.Should().Be("new-raw");
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsRevoked()
    {
        _tokenService.HashRefreshToken(Arg.Any<string>()).Returns("hash");
        _tokens.FindByHashAsync("hash", default).Returns((RefreshToken?)null);

        var result = await _sut.Handle(new RefreshTokenCommand("raw"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFRESH_TOKEN_REVOKED");
    }

    [Fact]
    public async Task Handle_RevokedToken_RevokesFamily_ReturnsReuse()
    {
        var userId = Guid.NewGuid();
        const string hash = "hashed";
        var stored = MakeValidToken(userId, hash);
        stored.Revoke(); // Revoke it — simulates reuse

        _tokenService.HashRefreshToken("raw").Returns(hash);
        _tokens.FindByHashAsync(hash, default).Returns(stored);

        var result = await _sut.Handle(new RefreshTokenCommand("raw"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFRESH_TOKEN_REUSE");
        await _tokens.Received(1).RevokeByFamilyAsync(stored.FamilyId, default);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsExpired()
    {
        var userId = Guid.NewGuid();
        var stored = new RefreshToken(userId, "hash", Guid.NewGuid(), ttlDays: -1); // already expired

        _tokenService.HashRefreshToken("raw").Returns("hash");
        _tokens.FindByHashAsync("hash", default).Returns(stored);

        var result = await _sut.Handle(new RefreshTokenCommand("raw"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFRESH_TOKEN_EXPIRED");
    }
}
