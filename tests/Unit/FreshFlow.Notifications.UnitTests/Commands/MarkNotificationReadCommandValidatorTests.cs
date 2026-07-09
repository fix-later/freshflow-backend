using FluentAssertions;
using FreshFlow.Notifications.Application.Commands.MarkNotificationRead;

namespace FreshFlow.Notifications.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class MarkNotificationReadCommandValidatorTests
{
    private readonly MarkNotificationReadCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new MarkNotificationReadCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new MarkNotificationReadCommand(Guid.Empty, Guid.NewGuid()));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(MarkNotificationReadCommand.UserId));
    }

    [Fact]
    public async Task Validate_EmptyNotificationId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new MarkNotificationReadCommand(Guid.NewGuid(), Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(MarkNotificationReadCommand.NotificationId));
    }
}
