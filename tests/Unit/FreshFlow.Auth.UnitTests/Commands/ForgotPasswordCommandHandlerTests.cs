using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.ForgotPassword;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository _resetTokens =
        Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordResetSender _sender = Substitute.For<IPasswordResetSender>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ForgotPasswordCommandHandler _sut;

    public ForgotPasswordCommandHandlerTests()
    {
        _tokenService.GenerateRefreshToken().Returns("rawtoken123");
        _tokenService.HashRefreshToken("rawtoken123").Returns("hashedtoken123");
        _sut = new ForgotPasswordCommandHandler(_users, _resetTokens, _sender, _tokenService);
    }

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsSuccessWithoutCreatingToken()
    {
        // Arrange
        _users.FindByEmailAsync(Arg.Any<string>(), default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new ForgotPasswordCommand("unknown@example.com"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _resetTokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), default);
        await _resetTokens.DidNotReceive().SaveChangesAsync(default);
        await _sender.DidNotReceive().SendResetLinkAsync(Arg.Any<string>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsSuccessWithoutCreatingToken()
    {
        // Arrange
        var adminRole = new Role("admin", "Administrator");
        var user = User.Create("inactive@example.com", "hash", adminRole);
        user.Deactivate();
        _users.FindByEmailAsync("inactive@example.com", default).Returns(user);

        // Act
        var result = await _sut.Handle(new ForgotPasswordCommand("inactive@example.com"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _resetTokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), default);
        await _sender.DidNotReceive().SendResetLinkAsync(Arg.Any<string>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_ActiveUser_InvalidatesPreviousTokenCreatesNewOneAndSendsEmail()
    {
        // Arrange
        var adminRole = new Role("admin", "Administrator");
        var user = User.Create("manager@example.com", "hash", adminRole);
        _users.FindByEmailAsync("manager@example.com", default).Returns(user);

        // Act
        var result = await _sut.Handle(new ForgotPasswordCommand("manager@example.com"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _resetTokens.Received(1).InvalidatePendingAsync(user.Id, default);
        await _resetTokens.Received(1).AddAsync(Arg.Any<PasswordResetToken>(), default);
        await _resetTokens.Received(1).SaveChangesAsync(default);
        await _sender.Received(1).SendResetLinkAsync("manager@example.com", "rawtoken123", default);
    }

    [Fact]
    public async Task Handle_ActiveUser_NormalizesEmailBeforeLookup()
    {
        // Arrange
        _users.FindByEmailAsync("user@example.com", default).Returns((User?)null);

        // Act
        await _sut.Handle(new ForgotPasswordCommand("USER@EXAMPLE.COM"), default);

        // Assert
        await _users.Received(1).FindByEmailAsync("user@example.com", default);
    }
}
