using FluentAssertions;
using FreshFlow.Auth.Application.Commands.ResetPassword;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _sut = new();

    [Theory]
    [InlineData("", "NewP@ss1")]
    [InlineData("valid-token", "")]
    [InlineData("valid-token", "short")]
    [InlineData("valid-token", "nouppercase1!")]
    [InlineData("valid-token", "NODIGIT!Abc")]
    [InlineData("valid-token", "NoSpecial1Abc")]
    public async Task Validate_InvalidInput_Fails(string token, string newPassword)
    {
        var result = await _sut.ValidateAsync(new ResetPasswordCommand(token, newPassword));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidInput_Passes()
    {
        var result = await _sut.ValidateAsync(new ResetPasswordCommand("some-token", "NewP@ss1"));
        result.IsValid.Should().BeTrue();
    }
}
