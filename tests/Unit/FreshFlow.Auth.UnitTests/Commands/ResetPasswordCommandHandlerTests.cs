using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.ResetPassword;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordResetTokenRepository _resetTokens = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ResetPasswordCommandHandler _sut;

    private static Role AdminRole() => new("admin", "Administrator");

    public ResetPasswordCommandHandlerTests()
    {
        _tokenService.HashRefreshToken("raw-token").Returns("token-hash");
        _hasher.Hash(Arg.Any<string>()).Returns("new-password-hash");
        _sut = new ResetPasswordCommandHandler(_users, _tokens, _resetTokens, _hasher, _tokenService);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsTokenInvalid()
    {
        _resetTokens.FindByHashAsync("token-hash", default).Returns((PasswordResetToken?)null);

        var result = await _sut.Handle(new ResetPasswordCommand("raw-token", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_TOKEN_INVALID");
    }

    [Fact]
    public async Task Handle_UsedToken_ReturnsTokenExpired()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "token-hash");
        token.MarkUsed();
        _resetTokens.FindByHashAsync("token-hash", default).Returns(token);

        var result = await _sut.Handle(new ResetPasswordCommand("raw-token", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_TOKEN_EXPIRED");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsTokenExpired()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "token-hash");
        // Force expiry via reflection
        typeof(PasswordResetToken)
            .GetProperty(nameof(PasswordResetToken.ExpiresAt))!
            .SetValue(token, DateTime.UtcNow.AddMinutes(-1));
        _resetTokens.FindByHashAsync("token-hash", default).Returns(token);

        var result = await _sut.Handle(new ResetPasswordCommand("raw-token", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_TOKEN_EXPIRED");
    }

    [Fact]
    public async Task Handle_ValidToken_ChangesPasswordRevokesTokensAndMarksUsed()
    {
        var userId = Guid.NewGuid();
        var token = PasswordResetToken.Create(userId, "token-hash");
        var user = User.Create("user@test.com", "old-hash", AdminRole());
        _resetTokens.FindByHashAsync("token-hash", default).Returns(token);
        _users.FindByIdAsync(userId, default).Returns(user);

        var result = await _sut.Handle(new ResetPasswordCommand("raw-token", "NewP@ss1"), default);

        result.IsSuccess.Should().BeTrue();
        token.IsUsed.Should().BeTrue();
        user.PasswordHash.Should().Be("new-password-hash");
        await _tokens.Received(1).RevokeByUserAsync(user.Id, "PASSWORD_RESET", default);
        await _users.Received(1).SaveChangesAsync(default);
    }
}
