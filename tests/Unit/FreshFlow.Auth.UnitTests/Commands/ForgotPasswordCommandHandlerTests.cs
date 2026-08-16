using System.Net.Http;
using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.ForgotPassword;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ForgotPasswordCommandHandlerTests
{
    // NSubstitute cannot proxy ILogger<InternalClass> (Castle.Core restriction on strong-named assemblies).
    // Use a minimal capturing logger instead.
    private sealed class CapturingLogger : ILogger<ForgotPasswordCommandHandler>
    {
        public readonly List<LogLevel> CapturedLevels = [];
        public readonly List<string> CapturedMessages = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            CapturedLevels.Add(logLevel);
            CapturedMessages.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository _resetTokens =
        Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordResetSender _sender = Substitute.For<IPasswordResetSender>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly CapturingLogger _logger = new();
    private readonly ForgotPasswordCommandHandler _sut;

    public ForgotPasswordCommandHandlerTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-code");
        _sut = new ForgotPasswordCommandHandler(_users, _resetTokens, _sender, _passwordHasher, _logger);
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
        await _sender.DidNotReceive().SendResetCodeAsync(Arg.Any<string>(), Arg.Any<string>(), default);
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
        await _sender.DidNotReceive().SendResetCodeAsync(Arg.Any<string>(), Arg.Any<string>(), default);
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
        await _sender.Received(1).SendResetCodeAsync("manager@example.com",
            Arg.Is<string>(code => code.Length == 6 && code.All(char.IsDigit)), default);
    }

    [Fact]
    public async Task Handle_SenderThrows_StillReturnsSuccess()
    {
        // Arrange
        var adminRole = new Role("admin", "Administrator");
        var user = User.Create("owner@test.vn", "hash", adminRole);
        _users.FindByEmailAsync("owner@test.vn", default).Returns(user);
        _sender
            .When(s => s.SendResetCodeAsync(Arg.Any<string>(), Arg.Any<string>(), default))
            .Do(_ => throw new HttpRequestException("Resend unavailable"));

        // Act
        var result = await _sut.Handle(new ForgotPasswordCommand("owner@test.vn"), default);

        // Assert — delivery failure must not surface to caller (anti-oracle)
        result.IsSuccess.Should().BeTrue();
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

    // M8 — ForgotPassword: delivery failure must be logged at Warning with userId context
    [Fact]
    public async Task Handle_SenderThrows_LogsWarningWithUserId()
    {
        // Arrange
        var role = new Role("admin", "Administrator");
        var user = User.Create("owner@test.vn", "hash", role);
        _users.FindByEmailAsync("owner@test.vn", default).Returns(user);
        _sender
            .When(s => s.SendResetCodeAsync(Arg.Any<string>(), Arg.Any<string>(), default))
            .Do(_ => throw new HttpRequestException("Resend unavailable"));

        // Act
        var result = await _sut.Handle(new ForgotPasswordCommand("owner@test.vn"), default);

        // Assert — warning logged AND result is still success (anti-oracle)
        result.IsSuccess.Should().BeTrue();
        _logger.CapturedLevels.Should().Contain(LogLevel.Warning,
            "a Warning should be logged when email delivery fails");
        _logger.CapturedMessages.Should().ContainMatch($"*{user.Id}*",
            "log message must include the userId for observability without leaking PII");
    }
}
