using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Admin.AssignRole;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class AssignRoleCommandValidatorTests
{
    private readonly AssignRoleCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyRoleName_Fails(string roleName)
    {
        var result = await _sut.ValidateAsync(new AssignRoleCommand(Guid.NewGuid(), roleName));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssignRoleCommand.RoleName));
    }

    [Fact]
    public async Task Validate_EmptyUserId_Fails()
    {
        var result = await _sut.ValidateAsync(new AssignRoleCommand(Guid.Empty, "driver"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssignRoleCommand.UserId));
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(new AssignRoleCommand(Guid.NewGuid(), "hub_staff"));

        result.IsValid.Should().BeTrue();
    }
}
