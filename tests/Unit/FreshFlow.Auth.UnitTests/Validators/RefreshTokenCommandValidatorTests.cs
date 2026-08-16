using FluentAssertions;
using FreshFlow.Auth.Application.Commands.RefreshToken;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyRefreshToken_Fails(string token)
    {
        var result = await _sut.ValidateAsync(new RefreshTokenCommand(token));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RefreshTokenCommand.RefreshToken));
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(new RefreshTokenCommand("valid-token"));
        result.IsValid.Should().BeTrue();
    }
}
