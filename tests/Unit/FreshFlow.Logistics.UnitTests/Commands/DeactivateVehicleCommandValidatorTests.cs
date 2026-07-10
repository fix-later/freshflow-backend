using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeactivateVehicle;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeactivateVehicleCommandValidatorTests
{
    [Fact]
    public void Validate_EmptyId_Fails()
    {
        var sut = new DeactivateVehicleCommandValidator();

        var result = sut.Validate(new DeactivateVehicleCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
