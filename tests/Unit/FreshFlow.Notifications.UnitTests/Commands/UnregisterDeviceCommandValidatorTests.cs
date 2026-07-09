using FluentAssertions;
using FreshFlow.Notifications.Application.Commands.UnregisterDevice;

namespace FreshFlow.Notifications.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UnregisterDeviceCommandValidatorTests
{
    private readonly UnregisterDeviceCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new UnregisterDeviceCommand(Guid.NewGuid(), "push-token"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Validate_BlankToken_FailsAsync(string? token)
    {
        var result = await _sut.ValidateAsync(
            new UnregisterDeviceCommand(Guid.NewGuid(), token!));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UnregisterDeviceCommand.Token));
    }
}
