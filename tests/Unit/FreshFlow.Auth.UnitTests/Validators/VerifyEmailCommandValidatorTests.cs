using FluentAssertions;
using FreshFlow.Auth.Application.Commands.VerifyEmail;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class VerifyEmailCommandValidatorTests
{
    private readonly VerifyEmailCommandValidator _sut = new();

    [Theory]
    [InlineData("", "EMAIL", "123456")]
    [InlineData("not-an-email", "EMAIL", "123456")]
    [InlineData("u@test.com", "", "123456")]
    [InlineData("u@test.com", "EMAIL", "")]
    public async Task Validate_InvalidInput_Fails(string identifier, string channel, string code)
    {
        var result = await _sut.ValidateAsync(new VerifyEmailCommand(identifier, channel, code));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidInputs_Passes()
    {
        var result = await _sut.ValidateAsync(new VerifyEmailCommand("u@test.com", "EMAIL", "482915"));
        result.IsValid.Should().BeTrue();
    }
}
