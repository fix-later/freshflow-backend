using FluentAssertions;
using FreshFlow.Auth.Application.Commands.ForgotPassword;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public async Task Validate_EmptyIdentifier_Fails(string? identifier)
    {
        var result = await _sut.ValidateAsync(new ForgotPasswordCommand(identifier!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ForgotPasswordCommand.Identifier));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public async Task Validate_NonEmailIdentifier_Fails(string identifier)
    {
        var result = await _sut.ValidateAsync(new ForgotPasswordCommand(identifier));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ForgotPasswordCommand.Identifier));
    }

    [Fact]
    public async Task Validate_ValidEmail_Passes()
    {
        var result = await _sut.ValidateAsync(new ForgotPasswordCommand("manager@phobaatu.vn"));

        result.IsValid.Should().BeTrue();
    }
}
