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
    private readonly ResetPasswordCommandHandler _sut;

    private static Role AdminRole() => new("admin", "Administrator");

    public ResetPasswordCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("new-password-hash");
        _hasher.Verify("123456", "otp-hash").Returns(true);
        _sut = new ResetPasswordCommandHandler(_users, _tokens, _resetTokens, _hasher);
    }

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsOtpInvalid()
    {
        _users.FindByEmailAsync("user@test.com", default).Returns((User?)null);

        var result = await _sut.Handle(new ResetPasswordCommand("user@test.com", "123456", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_OTP_INVALID");
    }

    [Fact]
    public async Task Handle_UsedOtp_ReturnsOtpInvalid()
    {
        var user = User.Create("user@test.com", "old-hash", AdminRole());
        var token = PasswordResetToken.Create(user.Id, "otp-hash");
        token.MarkUsed();
        _users.FindByEmailAsync(user.Email, default).Returns(user);
        _resetTokens.FindLatestPendingByUserIdAsync(user.Id, default).Returns(token);

        var result = await _sut.Handle(new ResetPasswordCommand(user.Email, "123456", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_OTP_INVALID");
    }

    [Fact]
    public async Task Handle_ExpiredOtp_ReturnsOtpInvalid()
    {
        var user = User.Create("user@test.com", "old-hash", AdminRole());
        var token = PasswordResetToken.Create(user.Id, "otp-hash");
        // Force expiry via reflection
        typeof(PasswordResetToken)
            .GetProperty(nameof(PasswordResetToken.ExpiresAt))!
            .SetValue(token, DateTime.UtcNow.AddMinutes(-1));
        _users.FindByEmailAsync(user.Email, default).Returns(user);
        _resetTokens.FindLatestPendingByUserIdAsync(user.Id, default).Returns(token);

        var result = await _sut.Handle(new ResetPasswordCommand(user.Email, "123456", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_OTP_INVALID");
    }

    [Fact]
    public async Task Handle_IncorrectOtp_ReturnsOtpInvalid()
    {
        var user = User.Create("user@test.com", "old-hash", AdminRole());
        var token = PasswordResetToken.Create(user.Id, "otp-hash");
        _users.FindByEmailAsync(user.Email, default).Returns(user);
        _resetTokens.FindLatestPendingByUserIdAsync(user.Id, default).Returns(token);
        _hasher.Verify("000000", "otp-hash").Returns(false);

        var result = await _sut.Handle(new ResetPasswordCommand(user.Email, "000000", "NewP@ss1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RESET_OTP_INVALID");
    }

    [Fact]
    public async Task Handle_ValidOtp_ChangesPasswordRevokesTokensAndMarksUsed()
    {
        var user = User.Create("user@test.com", "old-hash", AdminRole());
        var token = PasswordResetToken.Create(user.Id, "otp-hash");
        _users.FindByEmailAsync(user.Email, default).Returns(user);
        _resetTokens.FindLatestPendingByUserIdAsync(user.Id, default).Returns(token);

        var result = await _sut.Handle(new ResetPasswordCommand(user.Email, "123456", "NewP@ss1"), default);

        result.IsSuccess.Should().BeTrue();
        token.IsUsed.Should().BeTrue();
        user.PasswordHash.Should().Be("new-password-hash");
        await _tokens.Received(1).RevokeByUserAsync(user.Id, "PASSWORD_RESET", default);
        await _users.Received(1).SaveChangesAsync(default);
    }
}
