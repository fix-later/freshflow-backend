using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Logout;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyRefreshToken_Fails(string token)
    {
        var result = await _sut.ValidateAsync(new LogoutCommand(Guid.NewGuid(), token));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LogoutCommand.RefreshToken));
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(new LogoutCommand(Guid.NewGuid(), "valid-token"));
        result.IsValid.Should().BeTrue();
    }
}
