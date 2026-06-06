using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Admin.ActivateUser;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class ActivateUserCommandValidatorTests
{
    private readonly ActivateUserCommandValidator _sut = new();

    [Fact]
    public async Task Validate_EmptyUserId_Fails()
    {
        var result = await _sut.ValidateAsync(new ActivateUserCommand(Guid.Empty, true, Guid.NewGuid()));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ActivateUserCommand.UserId));
    }

    [Fact]
    public async Task Validate_EmptyRequestingAdminId_Fails()
    {
        var result = await _sut.ValidateAsync(new ActivateUserCommand(Guid.NewGuid(), true, Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ActivateUserCommand.RequestingAdminId));
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(new ActivateUserCommand(Guid.NewGuid(), true, Guid.NewGuid()));
        result.IsValid.Should().BeTrue();
    }
}
