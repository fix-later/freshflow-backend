using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Admin.UnlockUser;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class UnlockUserCommandValidatorTests
{
    private readonly UnlockUserCommandValidator _sut = new();

    [Fact]
    public async Task Validate_EmptyUserId_Fails()
    {
        var result = await _sut.ValidateAsync(new UnlockUserCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UnlockUserCommand.UserId));
    }

    [Fact]
    public async Task Validate_ValidUserId_Passes()
    {
        var result = await _sut.ValidateAsync(new UnlockUserCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
