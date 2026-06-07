using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.VerifyEmail;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class VerifyEmailCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IVerificationCodeRepository _codes = Substitute.For<IVerificationCodeRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly VerifyEmailCommandHandler _sut;

    private static Role AdminRole() => new("admin", "Administrator");

    public VerifyEmailCommandHandlerTests()
    {
        _tokenService.HashRefreshToken(Arg.Any<string>()).Returns("codeHash");
        _sut = new VerifyEmailCommandHandler(_users, _codes, _tokenService);
    }

    [Fact]
    public async Task Handle_PhoneChannel_ReturnsChannelNotSupported()
    {
        var result = await _sut.Handle(new VerifyEmailCommand("u@test.com", "PHONE", "123456"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CHANNEL_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsOtpInvalid()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), default).Returns((User?)null);

        var result = await _sut.Handle(new VerifyEmailCommand("unknown@test.com", "EMAIL", "123456"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("OTP_INVALID");
    }

    [Fact]
    public async Task Handle_AlreadyVerified_ReturnsSuccess()
    {
        var user = User.Create("user@test.com", "hash", AdminRole());
        user.MarkEmailVerified();
        _users.FindByEmailAsync("user@test.com", default).Returns(user);

        var result = await _sut.Handle(new VerifyEmailCommand("user@test.com", "EMAIL", "123456"), default);

        result.IsSuccess.Should().BeTrue();
        await _codes.DidNotReceive().FindByUserChannelAndHashAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_CodeNotFound_ReturnsOtpInvalid()
    {
        var user = User.Create("user@test.com", "hash", AdminRole());
        _users.FindByEmailAsync("user@test.com", default).Returns(user);
        _codes.FindByUserChannelAndHashAsync(user.Id, "EMAIL", "codeHash", default)
            .Returns((VerificationCode?)null);

        var result = await _sut.Handle(new VerifyEmailCommand("user@test.com", "EMAIL", "123456"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("OTP_INVALID");
    }

    [Fact]
    public async Task Handle_UsedCode_ReturnsOtpInvalid()
    {
        var user = User.Create("user@test.com", "hash", AdminRole());
        _users.FindByEmailAsync("user@test.com", default).Returns(user);

        var usedCode = VerificationCode.Create(user.Id, "EMAIL", "codeHash");
        usedCode.MarkUsed();
        _codes.FindByUserChannelAndHashAsync(user.Id, "EMAIL", "codeHash", default).Returns(usedCode);

        var result = await _sut.Handle(new VerifyEmailCommand("user@test.com", "EMAIL", "123456"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("OTP_INVALID");
    }

    [Fact]
    public async Task Handle_ValidCode_MarksCodeUsedAndEmailVerified()
    {
        var user = User.Create("user@test.com", "hash", AdminRole());
        _users.FindByEmailAsync("user@test.com", default).Returns(user);

        var code = VerificationCode.Create(user.Id, "EMAIL", "codeHash");
        _codes.FindByUserChannelAndHashAsync(user.Id, "EMAIL", "codeHash", default).Returns(code);

        var result = await _sut.Handle(new VerifyEmailCommand("user@test.com", "EMAIL", "123456"), default);

        result.IsSuccess.Should().BeTrue();
        code.IsUsed.Should().BeTrue();
        user.EmailVerifiedAt.Should().NotBeNull();
        await _users.Received(1).SaveChangesAsync(default);
    }
}
