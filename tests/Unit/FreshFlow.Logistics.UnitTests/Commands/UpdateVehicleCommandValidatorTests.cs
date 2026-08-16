using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.UpdateVehicle;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateVehicleCommandValidatorTests
{
    private readonly UpdateVehicleCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new UpdateVehicleCommand(Guid.NewGuid(), "ABC-123", 1200, "van"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_Fails()
    {
        var result = _sut.Validate(new UpdateVehicleCommand(Guid.Empty, "ABC-123", 1200, "van"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidVehicleType_Fails()
    {
        var result = _sut.Validate(new UpdateVehicleCommand(Guid.NewGuid(), "ABC-123", 1200, "boat"));

        result.IsValid.Should().BeFalse();
    }
}
