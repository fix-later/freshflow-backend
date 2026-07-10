using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.AssignVehicle;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AssignVehicleCommandValidatorTests
{
    private readonly AssignVehicleCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new AssignVehicleCommand(Guid.NewGuid(), Guid.NewGuid(), null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var result = _sut.Validate(new AssignVehicleCommand(Guid.Empty, Guid.NewGuid(), null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyVehicleId_Fails()
    {
        var result = _sut.Validate(new AssignVehicleCommand(Guid.NewGuid(), Guid.Empty, null));

        result.IsValid.Should().BeFalse();
    }
}
