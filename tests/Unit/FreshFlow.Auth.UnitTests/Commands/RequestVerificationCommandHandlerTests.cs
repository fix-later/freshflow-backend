using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.RequestVerification;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RequestVerificationCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IVerificationCodeRepository _codes = Substitute.For<IVerificationCodeRepository>();
    private readonly IVerificationSender _sender = Substitute.For<IVerificationSender>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly RequestVerificationCommandHandler _sut;

    private static Role AdminRole() => new("admin", "Administrator");

    public RequestVerificationCommandHandlerTests()
    {
        _tokenService.HashRefreshToken(Arg.Any<string>()).Returns("codeHash");
        _sut = new RequestVerificationCommandHandler(_users, _codes, _sender, _tokenService);
    }

    [Fact]
    public async Task Handle_PhoneChannel_ReturnsChannelNotSupported()
    {
        var result = await _sut.Handle(new RequestVerificationCommand("u@test.com", "PHONE"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CHANNEL_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsSuccessWithoutSending()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), default).Returns((User?)null);

        var result = await _sut.Handle(new RequestVerificationCommand("unknown@test.com", "EMAIL"), default);

        result.IsSuccess.Should().BeTrue();
        await _codes.DidNotReceive().AddAsync(Arg.Any<VerificationCode>(), default);
        await _sender.DidNotReceive().SendVerificationCodeAsync(Arg.Any<string>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsSuccessWithoutSending()
    {
        var user = User.Create("inactive@test.com", "hash", AdminRole());
        user.Deactivate();
        _users.FindByEmailAsync("inactive@test.com", default).Returns(user);

        var result = await _sut.Handle(new RequestVerificationCommand("inactive@test.com", "EMAIL"), default);

        result.IsSuccess.Should().BeTrue();
        await _sender.DidNotReceive().SendVerificationCodeAsync(Arg.Any<string>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_ActiveUser_InvalidatesPendingCreatesCodeAndSendsEmail()
    {
        var user = User.Create("user@test.com", "hash", AdminRole());
        _users.FindByEmailAsync("user@test.com", default).Returns(user);

        var result = await _sut.Handle(new RequestVerificationCommand("user@test.com", "EMAIL"), default);

        result.IsSuccess.Should().BeTrue();
        await _codes.Received(1).InvalidatePendingAsync(user.Id, "EMAIL", default);
        await _codes.Received(1).AddAsync(Arg.Any<VerificationCode>(), default);
        await _codes.Received(1).SaveChangesAsync(default);
        await _sender.Received(1).SendVerificationCodeAsync("user@test.com", Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_ActiveUser_NormalizesEmailBeforeLookup()
    {
        _users.FindByEmailAsync("user@test.com", default).Returns((User?)null);

        await _sut.Handle(new RequestVerificationCommand("USER@TEST.COM", "EMAIL"), default);

        await _users.Received(1).FindByEmailAsync("user@test.com", default);
    }

    [Fact]
    public async Task Handle_CaseInsensitiveChannel_AcceptsEmailLowercase()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), default).Returns((User?)null);

        var result = await _sut.Handle(new RequestVerificationCommand("u@test.com", "email"), default);

        result.IsSuccess.Should().BeTrue();
    }
}
