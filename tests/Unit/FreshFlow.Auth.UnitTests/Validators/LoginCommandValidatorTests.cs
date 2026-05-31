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
    [InlineData("not-an-email")]
    public async Task Validate_InvalidEmail_Fails(string email)
    {
        var result = await _sut.ValidateAsync(new LoginCommand(email, "P@ss1"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public async Task Validate_EmptyPassword_Fails()
    {
        var result = await _sut.ValidateAsync(new LoginCommand("u@test.com", string.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(new LoginCommand("u@test.com", "anypassword"));
        result.IsValid.Should().BeTrue();
    }
}
