using FluentAssertions;
using FreshFlow.Auth.Application.Commands.ResetPassword;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _sut = new();

    [Theory]
    [InlineData("", "123456", "NewP@ss1")]
    [InlineData("not-an-email", "123456", "NewP@ss1")]
    [InlineData("user@test.com", "12345", "NewP@ss1")]
    [InlineData("user@test.com", "abcdef", "NewP@ss1")]
    [InlineData("user@test.com", "123456", "")]
    [InlineData("user@test.com", "123456", "short")]
    [InlineData("user@test.com", "123456", "nouppercase1!")]
    [InlineData("user@test.com", "123456", "NODIGIT!Abc")]
    [InlineData("user@test.com", "123456", "NoSpecial1Abc")]
    public async Task Validate_InvalidInput_Fails(string identifier, string code, string newPassword)
    {
        var result = await _sut.ValidateAsync(new ResetPasswordCommand(identifier, code, newPassword));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidInput_Passes()
    {
        var result = await _sut.ValidateAsync(new ResetPasswordCommand("user@test.com", "123456", "NewP@ss1"));
        result.IsValid.Should().BeTrue();
    }
}
