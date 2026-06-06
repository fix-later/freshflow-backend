using FluentAssertions;
using FreshFlow.Auth.Application.Commands.ChangePassword;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(
            new ChangePasswordCommand(Guid.NewGuid(), "OldP@ss1", "NewP@ss1"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_MissingCurrentPassword_Fails()
    {
        var result = await _sut.ValidateAsync(
            new ChangePasswordCommand(Guid.NewGuid(), string.Empty, "NewP@ss1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.CurrentPassword));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("nouppercase1!")]
    [InlineData("NoDigit!!")]
    [InlineData("NoSpecial1")]
    public async Task Validate_WeakNewPassword_Fails(string newPassword)
    {
        var result = await _sut.ValidateAsync(
            new ChangePasswordCommand(Guid.NewGuid(), "OldP@ss1", newPassword));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    [Fact]
    public async Task Validate_NewPasswordSameAsCurrentPassword_Fails()
    {
        var result = await _sut.ValidateAsync(
            new ChangePasswordCommand(Guid.NewGuid(), "SameP@ss1", "SameP@ss1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }
}
