using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Login;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email-nor-phone")]   // no @ and not just digits
    [InlineData("abc")]                       // too short to be a phone (<7 digits)
    public async Task Validate_InvalidIdentifier_Fails(string identifier)
    {
        var result = await _sut.ValidateAsync(new LoginCommand(identifier, "P@ss1"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Identifier));
    }

    [Fact]
    public async Task Validate_EmptyPassword_Fails()
    {
        var result = await _sut.ValidateAsync(new LoginCommand("u@test.com", string.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Theory]
    [InlineData("u@test.com")]           // email
    [InlineData("+84901234567")]          // Vietnamese phone E.164
    [InlineData("0901234567")]            // Vietnamese local phone
    [InlineData("1234567")]              // minimum 7-digit phone
    public async Task Validate_ValidIdentifier_Passes(string identifier)
    {
        var result = await _sut.ValidateAsync(new LoginCommand(identifier, "anypassword"));
        result.IsValid.Should().BeTrue();
    }
}
