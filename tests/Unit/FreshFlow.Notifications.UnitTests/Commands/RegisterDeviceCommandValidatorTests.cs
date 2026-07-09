using FluentAssertions;
using FreshFlow.Notifications.Application.Commands.RegisterDevice;

namespace FreshFlow.Notifications.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RegisterDeviceCommandValidatorTests
{
    private readonly RegisterDeviceCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new RegisterDeviceCommand(Guid.NewGuid(), "push-token", "ios", "device-1"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyToken_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new RegisterDeviceCommand(Guid.NewGuid(), " ", "android", null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.Token));
    }

    [Fact]
    public async Task Validate_InvalidPlatform_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new RegisterDeviceCommand(Guid.NewGuid(), "push-token", "windows", null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.Platform));
    }
}
